file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

with open(file_path, "rb") as f:
    content = f.read().decode("utf-8")

lines = content.splitlines()
for idx, line in enumerate(lines):
    if "Floating Toolbar" in line:
        print(f"Line {idx + 1}: {line.strip()}")
