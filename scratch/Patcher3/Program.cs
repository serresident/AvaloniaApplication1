using System;
using System.IO;

string basePath = @"c:\Users\adm\projects\AvaloniaApplication1\AvaloniaApplication1\Views\";
string sourcePath = Path.Combine(basePath, "DashboardPanel.cs");

string sourceContent = File.ReadAllText(sourcePath);

// Extract Overlays
string gridOverlayStart = "    public class GridOverlay : Control";
string selectionOverlayStart = "    public class SelectionOverlay : Control";
string panelStart = "    public class DashboardPanel : Panel";

int gridIdx = sourceContent.IndexOf(gridOverlayStart);
int selectionIdx = sourceContent.IndexOf(selectionOverlayStart);
int panelIdx = sourceContent.IndexOf(panelStart);

if (gridIdx != -1 && selectionIdx != -1 && panelIdx != -1)
{
    string usingStatements = sourceContent.Substring(0, gridIdx);
    // Include namespace up to the class
    int namespaceStart = usingStatements.IndexOf("namespace AvaloniaApplication1.Views");
    string header = usingStatements.Substring(0, namespaceStart + "namespace AvaloniaApplication1.Views\r\n{\r\n".Length);
    if (!header.Contains("\r\n{\r\n")) {
         header = usingStatements.Substring(0, namespaceStart + "namespace AvaloniaApplication1.Views\n{\n".Length);
    }
    
    // We can just construct them from scratch to avoid issues with exact string matching
    string gridOverlayClass = sourceContent.Substring(gridIdx, selectionIdx - gridIdx);
    string selectionOverlayClass = sourceContent.Substring(selectionIdx, panelIdx - selectionIdx);

    // Save Overlays
    File.WriteAllText(Path.Combine(basePath, @"Overlays\GridOverlay.cs"), header + gridOverlayClass + "}\n");
    File.WriteAllText(Path.Combine(basePath, @"Overlays\SelectionOverlay.cs"), header + selectionOverlayClass + "}\n");

    // Remove them from source
    string newSource = usingStatements + sourceContent.Substring(panelIdx);
    File.WriteAllText(sourcePath, newSource);
    Console.WriteLine("Overlays extracted.");
}
else
{
    Console.WriteLine("Failed to find boundaries.");
}
