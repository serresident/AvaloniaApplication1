# -*- coding: utf-8 -*-
import re

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

with open(file_path, 'rb') as f:
    raw = f.read()

if raw.startswith(b'\xef\xbb\xbf'):
    raw = raw[3:]

content = raw.decode('utf-8')

# 1. Replace the DragHandle border tags to add Tag property
# Let's replace the standard Border DragHandle declaration
content = content.replace(
    '<Border IsVisible="{Binding IsSelected}" Name="DragHandle" Background="#33000000" BorderBrush="#007ACC" BorderThickness="2" CornerRadius="8" Margin="5" ZIndex="100" ClipToBounds="False">',
    '<Border IsVisible="{Binding IsSelected}" Name="DragHandle" Background="#33000000" BorderBrush="#007ACC" BorderThickness="2" CornerRadius="8" Margin="5" ZIndex="100" ClipToBounds="False" Tag="{Binding #WidgetsItemsControl.DataContext}">'
)
# And the Pipe Border DragHandle declaration
content = content.replace(
    '<Border IsVisible="{Binding IsSelected}" Name="DragHandle" Background="Transparent" ZIndex="100" ClipToBounds="False">',
    '<Border IsVisible="{Binding IsSelected}" Name="DragHandle" Background="Transparent" ZIndex="100" ClipToBounds="False" Tag="{Binding #WidgetsItemsControl.DataContext}">'
)

# 2. Replace context menu commands to use PlacementTarget.((vm:DashboardViewModel)Tag)
content = content.replace(
    '#WidgetsItemsControl.((vm:DashboardViewModel)DataContext)',
    '$parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag)'
)

# 3. Restructure the Valve (Клапан / Задвижка) template layout to support 180 deg labels swap
old_valve_layout = """                                    <Border Background="Transparent" BorderThickness="0" Margin="0" ClipToBounds="False">
                                        <Grid RowDefinitions="*,Auto" ClipToBounds="False">
                                            <!-- Valve Graphic -->
                                            <Grid Grid.Row="0" Margin="0" ClipToBounds="False">
                                                <v:ValveControl 
                                                    ValveType="{Binding ValveType}"
                                                    IsOpen="{Binding IsOpen}"
                                                    Setpoint="{Binding Setpoint}"
                                                    Feedback="{Binding Feedback}"
                                                    HasFeedbackSource="{Binding HasFeedbackSource}"
                                                    IsVertical="{Binding IsVertical}"
                                                    ActuatorType="{Binding ActuatorType}"
                                                    ActiveColor="{Binding ActiveColor}"
                                                    InactiveColor="{Binding InactiveColor}"
                                                    Rotation="{Binding Rotation}"
                                                    IsAlarmFlashing="{Binding IsAlarmFlashing}"
                                                    IsAlarmFlashState="{Binding IsAlarmFlashState}"
                                                    ShowStaticAlarmIcon="{Binding ShowStaticAlarmIcon}"
                                                    ShowFeedbackBar="True" />
                                            </Grid>
                                            <!-- Title and Value -->
                                            <StackPanel Grid.Row="1" HorizontalAlignment="Center" Margin="0,0,0,5">
                                                <TextBlock Text="{Binding Title}" FontSize="12" Foreground="Gray" HorizontalAlignment="Center" />
                                                <TextBlock Text="{Binding DisplayValue}" FontSize="12" FontWeight="Bold" Foreground="{Binding CurrentColor, Converter={StaticResource HexToBrush}}" HorizontalAlignment="Center" />
                                            </StackPanel>
                                        </Grid>
                                    </Border>"""

new_valve_layout = """                                    <Border Background="Transparent" BorderThickness="0" Margin="0" ClipToBounds="False">
                                        <DockPanel Tag="{Binding Rotation}" ClipToBounds="False">
                                            <!-- Title and Value -->
                                            <StackPanel Tag="{Binding Rotation}" DockPanel.Dock="Bottom" HorizontalAlignment="Center" Margin="0,0,0,5">
                                                <StackPanel.Styles>
                                                    <Style Selector="StackPanel[Tag=180]">
                                                        <Setter Property="DockPanel.Dock" Value="Top" />
                                                    </Style>
                                                </StackPanel.Styles>
                                                <TextBlock Text="{Binding Title}" FontSize="12" Foreground="Gray" HorizontalAlignment="Center" />
                                                <TextBlock Text="{Binding DisplayValue}" FontSize="12" FontWeight="Bold" Foreground="{Binding CurrentColor, Converter={StaticResource HexToBrush}}" HorizontalAlignment="Center" />
                                            </StackPanel>

                                            <!-- Valve Graphic -->
                                            <Grid ClipToBounds="False">
                                                <v:ValveControl 
                                                    ValveType="{Binding ValveType}"
                                                    IsOpen="{Binding IsOpen}"
                                                    Setpoint="{Binding Setpoint}"
                                                    Feedback="{Binding Feedback}"
                                                    HasFeedbackSource="{Binding HasFeedbackSource}"
                                                    IsVertical="{Binding IsVertical}"
                                                    ActuatorType="{Binding ActuatorType}"
                                                    ActiveColor="{Binding ActiveColor}"
                                                    InactiveColor="{Binding InactiveColor}"
                                                    Rotation="{Binding Rotation}"
                                                    IsAlarmFlashing="{Binding IsAlarmFlashing}"
                                                    IsAlarmFlashState="{Binding IsAlarmFlashState}"
                                                    ShowStaticAlarmIcon="{Binding ShowStaticAlarmIcon}"
                                                    ShowFeedbackBar="True" />
                                            </Grid>
                                        </DockPanel>
                                    </Border>"""

# Replace in content (adjusting line endings first to simplify search)
content = content.replace('\r\n', '\n').replace('\r', '\n')
old_valve_layout_clean = old_valve_layout.replace('\r\n', '\n').replace('\r', '\n')
new_valve_layout_clean = new_valve_layout.replace('\r\n', '\n').replace('\r', '\n')

if old_valve_layout_clean in content:
    content = content.replace(old_valve_layout_clean, new_valve_layout_clean)
    print("Valve layout updated successfully!")
else:
    print("WARNING: Valve layout block not found for exact replacement!")

# 4. Add "Rotate" (Повернуть) to the Valve context menu
# We find the context menu inside Valve template and add the menu item.
# Let's locate the Valve template by looking for the valve DockPanel/ValveControl and the ContextMenu inside it.
# We can do this with a replacement of the menu block specifically for Valve.
old_valve_menu_part = """                                            <MenuItem Header="На задний план" Command="{Binding $parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).SendToBackCommand}" CommandParameter="{Binding}" />
                                            <Separator />
                                            <MenuItem Header="Свойства" Command="{Binding $parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).EditWidgetCommand}" CommandParameter="{Binding}" />"""

new_valve_menu_part = """                                            <MenuItem Header="На задний план" Command="{Binding $parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).SendToBackCommand}" CommandParameter="{Binding}" />
                                            <Separator />
                                            <MenuItem Header="Повернуть" Command="{Binding RotateCommand}" />
                                            <Separator />
                                            <MenuItem Header="Свойства" Command="{Binding $parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).EditWidgetCommand}" CommandParameter="{Binding}" />"""

old_valve_menu_part_clean = old_valve_menu_part.replace('\r\n', '\n').replace('\r', '\n')
new_valve_menu_part_clean = new_valve_menu_part.replace('\r\n', '\n').replace('\r', '\n')

# We can search for the context menu specifically under ValveWidgetViewModel.
# Since other view models don't have RotateCommand, we can replace the first occurrence or match it carefully.
# Let's check how many times old_valve_menu_part matches: it matches in all templates!
# So we must only replace it inside the Valve template.
# Let's find the position of "vm:ValveWidgetViewModel" and find the first old_valve_menu_part after it.
valve_idx = content.find("vm:ValveWidgetViewModel")
if valve_idx != -1:
    part_idx = content.find(old_valve_menu_part_clean, valve_idx)
    if part_idx != -1:
        content = content[:part_idx] + new_valve_menu_part_clean + content[part_idx + len(old_valve_menu_part_clean):]
        print("Valve context menu Rotate item added successfully!")
    else:
        print("WARNING: Valve context menu part not found after ValveWidgetViewModel!")
else:
    print("WARNING: vm:ValveWidgetViewModel DataType not found!")

# 5. Remove the Copy (📋) button from all floating toolbars
# Let's modify ContainerButton floating toolbar to keep only OpenContainerCommand
old_container_toolbar = """                                       <!-- Floating Toolbar -->
                                       <Border VerticalAlignment="Top" HorizontalAlignment="Center" Margin="0,-36,0,0" Height="28" Background="#2D2D30" BorderBrush="#3E3E42" BorderThickness="1" CornerRadius="4" Padding="4,2" ZIndex="105" BoxShadow="0 2 8 0 #80000000">
                                           <StackPanel Orientation="Horizontal" Spacing="6">
                                               <Button Command="{Binding OpenContainerCommand}" Content="➡️" ToolTip.Tip="Открыть контейнер" FontSize="12" Width="22" Height="22" Padding="0" Background="Transparent" Foreground="White" />
                                               <Separator Width="1" Background="#3E3E42" Margin="2,0" />
                                               <Button Content="📋" ToolTip.Tip="Копировать" FontSize="12" Width="22" Height="22" Padding="0" Background="Transparent" Foreground="White"
                                                       Command="{Binding $parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).DuplicateWidgetCommand}"
                                                       CommandParameter="{Binding}" />
                                           </StackPanel>
                                       </Border>"""

new_container_toolbar = """                                       <!-- Floating Toolbar -->
                                       <Border VerticalAlignment="Top" HorizontalAlignment="Center" Margin="0,-36,0,0" Height="28" Background="#2D2D30" BorderBrush="#3E3E42" BorderThickness="1" CornerRadius="4" Padding="4,2" ZIndex="105" BoxShadow="0 2 8 0 #80000000">
                                           <StackPanel Orientation="Horizontal" Spacing="6">
                                               <Button Command="{Binding OpenContainerCommand}" Content="➡️" ToolTip.Tip="Открыть контейнер" FontSize="12" Width="22" Height="22" Padding="0" Background="Transparent" Foreground="White" />
                                           </StackPanel>
                                       </Border>"""

# Try both forms of DuplicateWidgetCommand (with tag binding or with original name binding)
for binding_form in [
    '#WidgetsItemsControl.((vm:DashboardViewModel)DataContext).DuplicateWidgetCommand',
    '$parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).DuplicateWidgetCommand'
]:
    old_toolbar_test = old_container_toolbar.replace(
        'Command="{Binding $parent[ContextMenu].PlacementTarget.((vm:DashboardViewModel)Tag).DuplicateWidgetCommand}"',
        f'Command="{{Binding {binding_form}}}"'
    ).replace('\r\n', '\n').replace('\r', '\n')
    new_toolbar_clean = new_container_toolbar.replace('\r\n', '\n').replace('\r', '\n')
    if old_toolbar_test in content:
        content = content.replace(old_toolbar_test, new_toolbar_clean)
        print("ContainerButton floating toolbar updated successfully!")
        break

# For all other templates, we remove the floating toolbar Border entirely.
# Let's write a regex that matches the floating toolbar Border blocks:
# <Border VerticalAlignment="Top" ... ZIndex="105" ...> ... </Border> or similar.
# Wait, let's find the exact regex for the other borders.
# Let's inspect the borders:
# <Border VerticalAlignment="Top" HorizontalAlignment="Center" Margin="0,-36,0,0" Height="28" Background="#2D2D30" BorderBrush="#3E3E42" BorderThickness="1" CornerRadius="4" Padding="4,2" ZIndex="105" BoxShadow="0 2 8 0 #80000000">
# ...
# </Border>
# And for Pipe:
# <Border VerticalAlignment="Top" HorizontalAlignment="Right" Margin="0,-32,0,0" Height="26" Background="#2D2D30" BorderBrush="#3E3E42" BorderThickness="1" CornerRadius="4" Padding="4,2" BoxShadow="0 2 8 0 #80000000">
# ...
# </Border>

# We can search and remove using regex:
content = re.sub(
    r'\s*<!-- Floating Toolbar -->\s*<Border VerticalAlignment="Top"\s+HorizontalAlignment="Center"\s+Margin="0,-36,0,0"\s+Height="28"\s+Background="#2D2D30"\s+BorderBrush="#3E3E42"\s+BorderThickness="1"\s+CornerRadius="4"\s+Padding="4,2"\s+ZIndex="105"\s+BoxShadow="0\s+2\s+8\s+0\s+#80000000">.*?<Button Content="📋".*?</Border>',
    '',
    content,
    flags=re.DOTALL
)

content = re.sub(
    r'\s*<!-- Floating Toolbar -->\s*<Border VerticalAlignment="Top"\s+HorizontalAlignment="Right"\s+Margin="0,-32,0,0"\s+Height="26"\s+Background="#2D2D30"\s+BorderBrush="#3E3E42"\s+BorderThickness="1"\s+CornerRadius="4"\s+Padding="4,2"\s+BoxShadow="0\s+2\s+8\s+0\s+#80000000">.*?<Button Content="📋".*?</Border>',
    '',
    content,
    flags=re.DOTALL
)

# Also let's double check if any other floating toolbar remains
remaining_toolbars = content.count("Floating Toolbar")
print(f"Remaining 'Floating Toolbar' comments: {remaining_toolbars} (expected 1 for ContainerButton)")

# Write back in UTF-8 with BOM, using \r\n line endings to match Windows standard
out = b'\xef\xbb\xbf' + content.replace('\n', '\r\n').encode('utf-8')

with open(file_path, 'wb') as f:
    f.write(out)

print("XAML processing complete!")
