import os

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\MainWindow.axaml"

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
    
    # 1. Insert KeyBindings after Window.Resources block ends
    old_resources_end = "</Window.Resources>"
    new_bindings = """</Window.Resources>

    <Window.KeyBindings>
        <KeyBinding Gesture="Ctrl+E" Command="{Binding ToggleDesignModeCommand}" />
    </Window.KeyBindings>"""
    
    if old_resources_end in content:
        content = content.replace(old_resources_end, new_bindings, 1)
        print("Inserted KeyBindings.")
    else:
        print("Error: </Window.Resources> not found.")
        
    # 2. Update toggle button text to show hotkey
    old_button_strings = 'TrueString="Lock Runtime" FalseString="Edit Design"'
    new_button_strings = 'TrueString="Lock Runtime (Ctrl+E)" FalseString="Edit Design (Ctrl+E)"'
    
    if old_button_strings in content:
        content = content.replace(old_button_strings, new_button_strings, 1)
        print("Updated toggle button strings.")
    else:
        # Check if they are written in a different format
        print("Error: Button strings target not found.")
        
    # Write back
    out_bytes = (b'\xef\xbb\xbf' if has_bom else b'') + content.encode('utf-8')
    with open(file_path, "wb") as f_out:
        f_out.write(out_bytes)
        
    print("MainWindow.axaml updated successfully.")
except Exception as e:
    print(f"Error occurred: {e}")
