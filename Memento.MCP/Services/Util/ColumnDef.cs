namespace Memento.MCP.Services.Util;

/// <summary>表字段定义（用于 CreateTable）</summary>
public class ColumnDef
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "varchar(255)";
    public bool Nullable { get; set; } = true;
    public bool PrimaryKey { get; set; }
    public bool AutoIncrement { get; set; }
    public string? DefaultValue { get; set; }
    public string? Comment { get; set; }
}
