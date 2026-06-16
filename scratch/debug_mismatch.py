# -*- coding: utf-8 -*-
import sys

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read().replace('\r\n', '\n').replace('\r', '\n')

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
                                    </Border>""".replace('\r\n', '\n').replace('\r', '\n')

print(f"File content length: {len(content)}")
print(f"Target length: {len(old_valve_layout)}")

# Let's find a substring in content
start_idx = content.find('<Border Background="Transparent" BorderThickness="0" Margin="0" ClipToBounds="False">')
print(f"Found '<Border...>' in content at: {start_idx}")

if start_idx != -1:
    # Print 2000 characters from start_idx
    snippet = content[start_idx:start_idx + len(old_valve_layout)]
    print("Mismatches:")
    for i in range(min(len(snippet), len(old_valve_layout))):
        if snippet[i] != old_valve_layout[i]:
            print(f"Char {i} mismatch: file={repr(snippet[i])}, target={repr(old_valve_layout[i])}")
            # print surrounding context
            print(f"File context: {repr(snippet[max(0, i-20):i+20])}")
            print(f"Target context: {repr(old_valve_layout[max(0, i-20):i+20])}")
            break
