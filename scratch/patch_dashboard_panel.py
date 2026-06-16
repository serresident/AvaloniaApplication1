# -*- coding: utf-8 -*-

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardPanel.cs"

with open(file_path, 'rb') as f:
    raw = f.read()

content = raw.decode('utf-8')
content = content.replace('\r\n', '\n').replace('\r', '\n')

# 1. Patch CollectionChanged handler
old_collection_changed_new_items = """                if (e.NewItems != null)
                {
                    foreach (Control child in e.NewItems)
                    {
                        child.DataContextChanged += Child_DataContextChanged;
                        SubscribeVm(child.DataContext as WidgetViewModelBase);
                        child.InvalidateMeasure();
                        child.InvalidateVisual();
                    }
                }"""

new_collection_changed_new_items = """                if (e.NewItems != null)
                {
                    foreach (Control child in e.NewItems)
                    {
                        child.DataContextChanged += Child_DataContextChanged;
                        var vm = child.DataContext as WidgetViewModelBase;
                        SubscribeVm(vm);
                        if (vm != null && vm.IsSelected)
                        {
                            SelectedVm = vm;
                        }
                        child.InvalidateMeasure();
                        child.InvalidateVisual();
                    }
                }

                // Clean orphaned selection reference
                if (SelectedVm != null)
                {
                    bool found = false;
                    foreach (var child in Children)
                    {
                        if (child.DataContext == SelectedVm)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        SelectedVm = null;
                    }
                }"""

if old_collection_changed_new_items in content:
    content = content.replace(old_collection_changed_new_items, new_collection_changed_new_items)
    print("CollectionChanged handler patched successfully!")
else:
    print("WARNING: CollectionChanged handler not found!")

# 2. Patch Child_DataContextChanged
old_data_context_changed = """        private void Child_DataContextChanged(object? sender, EventArgs e)
        {
            if (sender is Control child)
            {
                SubscribeVm(child.DataContext as WidgetViewModelBase);
                child.InvalidateMeasure();
                child.InvalidateVisual();
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
            }
        }"""

new_data_context_changed = """        private void Child_DataContextChanged(object? sender, EventArgs e)
        {
            if (sender is Control child)
            {
                var vm = child.DataContext as WidgetViewModelBase;
                SubscribeVm(vm);
                if (vm != null && vm.IsSelected)
                {
                    SelectedVm = vm;
                }

                // Clean orphaned selection reference
                if (SelectedVm != null)
                {
                    bool found = false;
                    foreach (var c in Children)
                    {
                        if (c.DataContext == SelectedVm)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        SelectedVm = null;
                    }
                }

                child.InvalidateMeasure();
                child.InvalidateVisual();
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
            }
        }"""

if old_data_context_changed in content:
    content = content.replace(old_data_context_changed, new_data_context_changed)
    print("Child_DataContextChanged handler patched successfully!")
else:
    print("WARNING: Child_DataContextChanged handler not found!")

# 3. Patch OnPointerPressed right-click handler
old_pointer_pressed_right_click = """                        // Правый клик (ПКМ) — только выделяем виджет,
                        // НЕ блокируем событие, чтобы ContextMenu сработал
                        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                        {
                            // Не ставим e.Handled — событие всплывёт к Border.ContextMenu
                            return;
                        }"""

new_pointer_pressed_right_click = """                        // Правый клик (ПКМ) — выделяем виджет и программно открываем контекстное меню
                        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                        {
                            var dragHandle = FindDragHandleRecursive(child);
                            if (dragHandle != null && dragHandle.ContextMenu != null)
                            {
                                dragHandle.ContextMenu.Open(dragHandle);
                            }
                            e.Handled = true;
                            return;
                        }"""

if old_pointer_pressed_right_click in content:
    content = content.replace(old_pointer_pressed_right_click, new_pointer_pressed_right_click)
    print("OnPointerPressed right-click handler patched successfully!")
else:
    print("WARNING: OnPointerPressed right-click handler not found!")

# 4. Add FindDragHandleRecursive to the end of the class
old_end_of_class = """        private bool IsDragHandle(object? source)
        {
            if (source is Avalonia.Visual visual)
            {
                var current = visual;
                while (current != null)
                {
                    if (current is Control ctrl && ctrl.Name == "DragHandle")
                        return true;
                    current = current.GetVisualParent();
                }
            }
            return false;
        }
    }
}"""

new_end_of_class = """        private bool IsDragHandle(object? source)
        {
            if (source is Avalonia.Visual visual)
            {
                var current = visual;
                while (current != null)
                {
                    if (current is Control ctrl && ctrl.Name == "DragHandle")
                        return true;
                    current = current.GetVisualParent();
                }
            }
            return false;
        }

        private Border? FindDragHandleRecursive(Avalonia.Visual? visual)
        {
            if (visual == null) return null;
            if (visual is Border border && border.Name == "DragHandle") return border;
            foreach (var child in visual.GetVisualChildren())
            {
                var result = FindDragHandleRecursive(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}"""

if old_end_of_class in content:
    content = content.replace(old_end_of_class, new_end_of_class)
    print("FindDragHandleRecursive helper method added successfully!")
else:
    print("WARNING: End of class pattern not matched!")

# Save back using \r\n line endings
out = content.replace('\n', '\r\n').encode('utf-8')
with open(file_path, 'wb') as f:
    f.write(out)
