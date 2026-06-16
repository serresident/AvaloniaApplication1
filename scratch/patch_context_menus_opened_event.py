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
    
    # Replace <ContextMenu x:CompileBindings="False"> with <ContextMenu x:CompileBindings="False" Opened="ContextMenu_Opened">
    old_tag = '<ContextMenu x:CompileBindings="False">'
    new_tag = '<ContextMenu x:CompileBindings="False" Opened="ContextMenu_Opened">'
    
    count_repl = content.count(old_tag)
    content = content.replace(old_tag, new_tag)
    print(f"Replaced {count_repl} context menus.")
    
    # Write back
    out_bytes = (b'\xef\xbb\xbf' if has_bom else b'') + content.encode('utf-8')
    with open(file_path, "wb") as f_out:
        f_out.write(out_bytes)
        
    print("XAML updated successfully.")
except Exception as e:
    print(f"Error occurred: {e}")
