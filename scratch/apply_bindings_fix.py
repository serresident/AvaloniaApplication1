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
    
    # Replace Tag binding on DragHandles
    old_tag = 'Tag="{Binding #WidgetsItemsControl.DataContext}"'
    new_tag = 'Tag="{Binding $parent[v:DashboardView].DataContext}"'
    
    count_tag = content.count(old_tag)
    content = content.replace(old_tag, new_tag)
    print(f"Replaced {count_tag} DragHandle Tag bindings.")
    
    # Replace CellSize on PipeControl
    old_cell = 'CellSize="{Binding $parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).CellWidth}"'
    new_cell = 'CellSize="{Binding $parent[v:DashboardPanel].CellWidth}"'
    
    count_cell = content.count(old_cell)
    content = content.replace(old_cell, new_cell)
    print(f"Replaced {count_cell} PipeControl CellSize bindings.")
    
    # Write back
    out_bytes = (b'\xef\xbb\xbf' if has_bom else b'') + content.encode('utf-8')
    with open(file_path, "wb") as f_out:
        f_out.write(out_bytes)
        
    print("XAML patches successfully applied.")
except Exception as e:
    print(f"Error occurred: {e}")
