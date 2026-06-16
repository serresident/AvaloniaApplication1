# -*- coding: utf-8 -*-

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

with open(file_path, 'rb') as f:
    raw = f.read()

if raw.startswith(b'\xef\xbb\xbf'):
    raw = raw[3:]

content = raw.decode('utf-8')
content = content.replace('\r\n', '\n').replace('\r', '\n')

# Find ContainerButtonViewModel template
marker = 'DataType="{x:Type vm:ContainerButtonViewModel}"'
idx = content.find(marker)
if idx != -1:
    # Find the DragHandle border inside this template
    drag_handle_idx = content.find('Name="DragHandle"', idx)
    if drag_handle_idx != -1:
        # Find the Grid after DragHandle
        grid_idx = content.find('<Grid ClipToBounds="False">', drag_handle_idx)
        if grid_idx != -1:
            # Find the closing </Grid> of this Grid
            end_grid_idx = content.find('</Grid>', grid_idx)
            if end_grid_idx != -1:
                # Replace the inner content of this Grid
                old_grid_content = content[grid_idx:end_grid_idx + 7]
                new_grid_content = """<Grid ClipToBounds="False">
                                       <!-- Floating Toolbar -->
                                       <Border VerticalAlignment="Top" HorizontalAlignment="Center" Margin="0,-36,0,0" Height="28" Background="#2D2D30" BorderBrush="#3E3E42" BorderThickness="1" CornerRadius="4" Padding="4,2" ZIndex="105" BoxShadow="0 2 8 0 #80000000">
                                           <StackPanel Orientation="Horizontal" Spacing="6">
                                               <Button Command="{Binding OpenContainerCommand}" Content="➡️" ToolTip.Tip="Открыть контейнер" FontSize="12" Width="22" Height="22" Padding="0" Background="Transparent" Foreground="White" />
                                           </StackPanel>
                                       </Border>
                                       <TextBlock Text="◢" Foreground="#007ACC" FontSize="14" HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,4,2" IsHitTestVisible="False" />
                                   </Grid>"""
                content = content[:grid_idx] + new_grid_content + content[end_grid_idx + 7:]
                print("ContainerButton floating toolbar restored successfully!")
else:
    print("ERROR: ContainerButtonViewModel template not found!")

# Save back
out = b'\xef\xbb\xbf' + content.replace('\n', '\r\n').encode('utf-8')
with open(file_path, 'wb') as f:
    f.write(out)
