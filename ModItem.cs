using System.ComponentModel;

public class ModItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Name { get; set; }
    public string Path { get; set; }
    public string Type { get; set; }
    public string Size { get; set; }
    public string Author { get; set; } = "Unknown";
    public string Version { get; set; } = "1.0";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public string TypeDisplay
    {
        get
        {
            switch (Type)
            {
                case "Folder": return "Folder Mod";
                case "Pak": return "Pak Mod";
                case "PakBundle": return "Pak Bundle";
                default: return "Unknown";
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
