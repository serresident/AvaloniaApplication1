import re

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

try:
    with open(file_path, "rb") as f:
        raw = f.read()
    
    # Check for BOM
    has_bom = raw.startswith(b'\xef\xbb\xbf')
    if has_bom:
        content = raw[3:].decode('utf-8')
    else:
        content = raw.decode('utf-8')
        
    print(f"File size: {len(raw)} bytes. BOM: {has_bom}")
    
    # 1. Replace <ContextMenu> with <ContextMenu x:CompileBindings="False">
    content = content.replace('<ContextMenu>', '<ContextMenu x:CompileBindings="False">')
    print("Replaced <ContextMenu> tags.")
    
    # 2. Replace the cast-based command binding paths with simple runtime reflection paths
    old_prefix = '$parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).'
    new_prefix = '$parent[ContextMenu].PlacementTarget.Tag.'
    
    count_repl = content.count(old_prefix)
    content = content.replace(old_prefix, new_prefix)
    print(f"Replaced {count_repl} command binding paths.")
    
    # Write back
    out_bytes = (b'\xef\xbb\xbf' if has_bom else b'') + content.encode('utf-8')
    with open(file_path, "wb") as f_out:
        f_out.write(out_bytes)
        
    print("XAML updated successfully.")
except Exception as e:
    print(f"Error occurred: {e}")
