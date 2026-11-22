#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.ViewModels.Windows.Common;

namespace AvaloniaApplication1.Services.Debug
{
    public interface IScadaDebugBridge : IAsyncDisposable
    {
        Task StartAsync(int port = 5110);
        Task StopAsync();
        bool IsRunning { get; }
        int Port { get; }
    }

    /// <summary>
    /// Внутрипроцессный мост отладки (In-App SCADA Debug Bridge & MCP Endpoint).
    /// Предоставляет JSON-RPC 2.0 эндпоинт Model Context Protocol (/mcp) и легковесный REST API (/api/*)
    /// для прямого взаимодействия AI-агентов с тегами, симуляцией, режимами и авариями SCADA.
    /// Компилируется строго в #if DEBUG и не влияет на релизную сборку.
    /// </summary>
    public sealed class ScadaDebugBridge : IScadaDebugBridge
    {
        private readonly IDataCoreService _dataCore;
        private readonly IProjectContextService _projectContext;
        private readonly Lazy<MainViewModel>? _mainVm;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _listenerLoop;
        private readonly DateTime _startTime = DateTime.UtcNow;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public bool IsRunning => _listener?.IsListening ?? false;
        public int Port { get; private set; } = 5110;

        public ScadaDebugBridge(
            IDataCoreService dataCore,
            IProjectContextService projectContext,
            Lazy<MainViewModel>? mainVm = null)
        {
            _dataCore = dataCore ?? throw new ArgumentNullException(nameof(dataCore));
            _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
            _mainVm = mainVm;
        }

        public Task StartAsync(int preferredPort = 5110)
        {
            if (IsRunning) return Task.CompletedTask;

            int port = preferredPort;
            const int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    _listener.Start();
                    Port = port;
                    break;
                }
                catch (HttpListenerException)
                {
                    _listener?.Close();
                    _listener = null;
                    port++;
                    if (i == maxRetries - 1) throw;
                }
            }

            _cts = new CancellationTokenSource();
            _listenerLoop = Task.Run(() => ListenAsync(_cts.Token));
            System.Diagnostics.Debug.WriteLine($"[ScadaDebugBridge] Active on http://127.0.0.1:{Port}/ (MCP endpoint: /mcp)");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (_listener == null) return Task.CompletedTask;

            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // Подавление ошибок при остановке
            }
            finally
            {
                _listener = null;
                _cts?.Dispose();
                _cts = null;
            }
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            StopAsync();
            return ValueTask.CompletedTask;
        }

        private async Task ListenAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var listener = _listener;
                    if (listener == null || !listener.IsListening) break;
                    var context = await listener.GetContextAsync().ConfigureAwait(false);
                    _ = ProcessRequestAsync(context, ct);
                }
                catch
                {
                    // Завершение цикла при остановке или отмене
                    break;
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            var req = ctx.Request;
            var res = ctx.Response;

            // CORS headers for local tools
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            string path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";
            try
            {
                if (path.Equals("/mcp", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleMcpJsonRpcAsync(req, res, ct);
                    return;
                }

                if (path.Equals("/api/status", StringComparison.OrdinalIgnoreCase) && req.HttpMethod == "GET")
                {
                    await WriteJsonAsync(res, GetStatusPayload());
                    return;
                }

                if (path.Equals("/api/tags", StringComparison.OrdinalIgnoreCase) && req.HttpMethod == "GET")
                {
                    await WriteJsonAsync(res, GetTagsPayload());
                    return;
                }

                if (path.Equals("/api/tag/override", StringComparison.OrdinalIgnoreCase) && req.HttpMethod == "POST")
                {
                    string body = await ReadRequestBodyAsync(req, ct);
                    var node = JsonNode.Parse(body);
                    string? tagId = node?["tagId"]?.ToString();
                    var valNode = node?["value"];
                    object? parsedVal = ExtractValue(valNode);

                    if (!string.IsNullOrEmpty(tagId) && parsedVal != null)
                    {
                        _dataCore.ForceOverride(tagId, parsedVal);
                        await WriteJsonAsync(res, new { success = true, tagId, value = parsedVal });
                    }
                    else
                    {
                        res.StatusCode = 400;
                        await WriteJsonAsync(res, new { success = false, error = "tagId and value are required" });
                    }
                    return;
                }

                if (path.Equals("/api/tag/clear", StringComparison.OrdinalIgnoreCase) && req.HttpMethod == "POST")
                {
                    string body = await ReadRequestBodyAsync(req, ct);
                    var node = JsonNode.Parse(body);
                    string? tagId = node?["tagId"]?.ToString();
                    if (!string.IsNullOrEmpty(tagId))
                    {
                        _dataCore.ClearOverride(tagId);
                        await WriteJsonAsync(res, new { success = true, tagId });
                    }
                    else
                    {
                        res.StatusCode = 400;
                        await WriteJsonAsync(res, new { success = false, error = "tagId is required" });
                    }
                    return;
                }

                if (path.Equals("/api/design-mode", StringComparison.OrdinalIgnoreCase) && req.HttpMethod == "POST")
                {
                    string body = await ReadRequestBodyAsync(req, ct);
                    var node = JsonNode.Parse(body);
                    bool? enable = node?["enable"]?.GetValue<bool>();
                    if (enable.HasValue)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _projectContext.IsDesignMode = enable.Value;
                        });
                        await WriteJsonAsync(res, new { success = true, isDesignMode = enable.Value });
                    }
                    else
                    {
                        res.StatusCode = 400;
                        await WriteJsonAsync(res, new { success = false, error = "enable boolean is required" });
                    }
                    return;
                }

                // Default 404
                res.StatusCode = 404;
                await WriteJsonAsync(res, new { error = "Not found", endpoints = new[] { "/mcp", "/api/status", "/api/tags", "/api/tag/override", "/api/tag/clear", "/api/design-mode" } });
            }
            catch (Exception ex)
            {
                res.StatusCode = 500;
                await WriteJsonAsync(res, new { error = ex.Message });
            }
            finally
            {
                res.Close();
            }
        }

        private async Task HandleMcpJsonRpcAsync(HttpListenerRequest req, HttpListenerResponse res, CancellationToken ct)
        {
            if (req.HttpMethod != "POST")
            {
                res.StatusCode = 405;
                await WriteJsonAsync(res, new { error = "MCP endpoint expects POST" });
                return;
            }

            string body = await ReadRequestBodyAsync(req, ct);
            var root = JsonNode.Parse(body);

            string? method = root?["method"]?.ToString();
            var id = root?["id"];

            if (method == "initialize")
            {
                var initResult = new
                {
                    protocolVersion = "2024-11-05",
                    serverInfo = new
                    {
                        name = "scada-debug-bridge",
                        version = "1.0.0"
                    },
                    capabilities = new
                    {
                        tools = new { listChanged = false }
                    }
                };
                await WriteJsonRpcResultAsync(res, id, initResult);
                return;
            }

            if (method == "tools/list")
            {
                var tools = new object[]
                {
                    new
                    {
                        name = "scada_get_status",
                        description = "Получить текущее состояние SCADA приложения: время работы, режим Design Mode, активный проект и количество тегов.",
                        inputSchema = new { type = "object", properties = new { } }
                    },
                    new
                    {
                        name = "scada_get_tags",
                        description = "Получить список зарегистрированных тегов SCADA, их текущие значения, статус качества (Good/Bad) и наличие ручного переопределения (Override).",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                filter = new { type = "string", description = "Опциональный фильтр по имени или идентификатору тега." }
                            }
                        }
                    },
                    new
                    {
                        name = "scada_set_tag",
                        description = "Принудительно установить (Force Override) значение технологического тега в DataCoreService для тестирования мнемосхемы, уставок, аварий или отклика виджетов.",
                        inputSchema = new
                        {
                            type = "object",
                            required = new[] { "tagId", "value" },
                            properties = new
                            {
                                tagId = new { type = "string", description = "Уникальный идентификатор тега (например: tag_t1, tag_l1, tag_plc1_40001)." },
                                value = new { description = "Устанавливаемое значение (число, строка или булево значение)." }
                            }
                        }
                    },
                    new
                    {
                        name = "scada_clear_override",
                        description = "Снять ручное переопределение с тега и вернуть его в штатный режим сетевого опроса.",
                        inputSchema = new
                        {
                            type = "object",
                            required = new[] { "tagId" },
                            properties = new
                            {
                                tagId = new { type = "string", description = "Идентификатор тега для снятия переопределения." }
                            }
                        }
                    },
                    new
                    {
                        name = "scada_toggle_design_mode",
                        description = "Переключить режим работы HMI: Design Mode (редактирование схемы, перемещение виджетов) или Runtime Mode (исполнение и операторский интерфейс).",
                        inputSchema = new
                        {
                            type = "object",
                            required = new[] { "enable" },
                            properties = new
                            {
                                enable = new { type = "boolean", description = "true — включить режим редактирования, false — перейти в рантайм." }
                            }
                        }
                    }
                };
                await WriteJsonRpcResultAsync(res, id, new { tools });
                return;
            }

            if (method == "tools/call")
            {
                string? toolName = root?["params"]?["name"]?.ToString();
                var args = root?["params"]?["arguments"];

                var toolOutput = ExecuteTool(toolName, args);
                var callResult = new
                {
                    content = new[]
                    {
                        new { type = "text", text = toolOutput }
                    }
                };
                await WriteJsonRpcResultAsync(res, id, callResult);
                return;
            }

            // Default unknown method
            await WriteJsonRpcErrorAsync(res, id, -32601, $"Method '{method}' not found");
        }

        private static async Task<string> ReadRequestBodyAsync(HttpListenerRequest req, CancellationToken ct)
        {
            long len = req.ContentLength64;
            if (len <= 0) return string.Empty;
            byte[] buffer = new byte[len];
            int totalRead = 0;
            while (totalRead < len)
            {
                int read = await req.InputStream.ReadAsync(buffer.AsMemory(totalRead, (int)len - totalRead), ct);
                if (read == 0) break;
                totalRead += read;
            }
            return (req.ContentEncoding ?? Encoding.UTF8).GetString(buffer, 0, totalRead);
        }

        private string ExecuteTool(string? toolName, JsonNode? args)
        {
            switch (toolName)
            {
                case "scada_get_status":
                    return JsonSerializer.Serialize(GetStatusPayload(), JsonOptions);

                case "scada_get_tags":
                    string? filter = args?["filter"]?.ToString();
                    var tags = GetTagsPayload(filter);
                    return JsonSerializer.Serialize(tags, JsonOptions);

                case "scada_set_tag":
                    string? tagId = args?["tagId"]?.ToString();
                    var valNode = args?["value"];
                    object? parsedVal = ExtractValue(valNode);
                    if (string.IsNullOrEmpty(tagId) || parsedVal == null)
                    {
                        return "Ошибка: tagId и value обязательны.";
                    }
                    _dataCore.ForceOverride(tagId, parsedVal);
                    return $"Тег '{tagId}' успешно переопределен на значение '{parsedVal}' (Quality: Good, IsOverridden: true).";

                case "scada_clear_override":
                    string? clrTagId = args?["tagId"]?.ToString();
                    if (string.IsNullOrEmpty(clrTagId))
                    {
                        return "Ошибка: tagId обязателен.";
                    }
                    _dataCore.ClearOverride(clrTagId);
                    return $"Переопределение с тега '{clrTagId}' снято, тег возвращен к штатному источнику.";

                case "scada_toggle_design_mode":
                    bool? enable = args?["enable"]?.GetValue<bool>();
                    if (!enable.HasValue)
                    {
                        return "Ошибка: параметр enable (bool) обязателен.";
                    }
                    _projectContext.IsDesignMode = enable.Value;
                    Dispatcher.UIThread.Post(() =>
                    {
                        _projectContext.IsDesignMode = enable.Value;
                    });
                    return $"Режим редактирования (IsDesignMode) установлен в: {enable.Value}.";

                default:
                    return $"Неизвестный инструмент: {toolName}";
            }
        }

        private object GetStatusPayload()
        {
            var uptime = DateTime.UtcNow - _startTime;
            string? activeProject = _mainVm?.Value?.CurrentProjectName;
            return new
            {
                app = "AvaloniaApplication1 HMI/SCADA",
                status = "Running",
                isDesignMode = _projectContext.IsDesignMode,
                activeProject = string.IsNullOrEmpty(activeProject) ? "default" : activeProject,
                uptimeSeconds = (int)uptime.TotalSeconds,
                totalTagsCount = _dataCore.TagManager.AllTags.Count,
                httpPort = Port
            };
        }

        private object GetTagsPayload(string? filter = null)
        {
            var all = (IEnumerable<TagDefinition>)_dataCore.TagManager.AllTags;
            if (!string.IsNullOrEmpty(filter))
            {
                all = all.Where(t => t.TagId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                     t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            return all.Select(t =>
            {
                var snap = _dataCore.GetTagSnapshot(t.TagId);
                return new
                {
                    tagId = t.TagId,
                    name = t.Name,
                    connId = t.ConnId,
                    address = t.Address,
                    dataType = t.DataType.ToString(),
                    unit = t.Unit,
                    value = snap.Value,
                    quality = snap.Quality.ToString(),
                    isOverridden = snap.IsOverridden,
                    timestamp = snap.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
            }).ToList();
        }

        private static object? ExtractValue(JsonNode? node)
        {
            if (node == null) return null;
            if (node is JsonValue jval)
            {
                if (jval.TryGetValue<bool>(out bool b)) return b;
                if (jval.TryGetValue<double>(out double d)) return d;
                if (jval.TryGetValue<long>(out long l)) return l;
                return jval.ToString();
            }
            return node.ToString();
        }

        private static async Task WriteJsonAsync(HttpListenerResponse res, object payload)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = bytes.Length;
            await res.OutputStream.WriteAsync(bytes);
        }

        private static async Task WriteJsonRpcResultAsync(HttpListenerResponse res, JsonNode? id, object result)
        {
            var resp = new
            {
                jsonrpc = "2.0",
                id = id,
                result = result
            };
            await WriteJsonAsync(res, resp);
        }

        private static async Task WriteJsonRpcErrorAsync(HttpListenerResponse res, JsonNode? id, int code, string message)
        {
            var resp = new
            {
                jsonrpc = "2.0",
                id = id,
                error = new { code, message }
            };
            await WriteJsonAsync(res, resp);
        }
    }
}
#endif
