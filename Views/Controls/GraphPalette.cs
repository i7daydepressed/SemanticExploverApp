using Avalonia.Media;
using Avalonia.Styling;

namespace SemanticExploverApp.Views.Controls;

internal sealed class GraphPalette { // цвета графа по фактической теме
    // граф и легенда используют эти же палитры, кисти не создаем заново при каждой перерисовке
    public static GraphPalette Light { get; } = new GraphPalette(false);
    public static GraphPalette Dark { get; } = new GraphPalette(true);
    public IBrush Background { get; }
    public IBrush Text { get; }
    public IBrush MutedText { get; }
    public IBrush Edge { get; }
    public IBrush Active { get; }
    public IBrush Selection { get; }
    public IBrush ClassFill { get; }
    public IBrush ClassBorder { get; }
    public IBrush InstanceFill { get; }
    public IBrush InstanceBorder { get; }
    public IBrush ValueFill { get; }
    public IBrush ValueBorder { get; }

    // собираем согласованные цвета фона, текста и типов для одной темы
    private GraphPalette(bool dark) {
        Background = Brush.Parse(dark ? "#111827" : "#F8FAFC");
        Text = Brush.Parse(dark ? "#E5E7EB" : "#0F172A");
        MutedText = Brush.Parse(dark ? "#B2BECE" : "#475569");
        Edge = Brush.Parse(dark ? "#748399" : "#64748B");
        Active = Brush.Parse(dark ? "#4ADE80" : "#15803D");
        Selection = Brush.Parse(dark ? "#FBBF24" : "#B45309");
        ClassFill = Brush.Parse(dark ? "#17283E" : "#EFF6FF");
        ClassBorder = Brush.Parse(dark ? "#60A5FA" : "#2563EB");
        InstanceFill = Brush.Parse(dark ? "#142F28" : "#ECFDF5");
        InstanceBorder = Brush.Parse(dark ? "#4ADE80" : "#15803D");
        ValueFill = Brush.Parse(dark ? "#35271B" : "#FFF7ED");
        ValueBorder = Brush.Parse(dark ? "#FDBA74" : "#C2410C");
    }

    // ищем светлую или темную основу, включая наследуемые темы
    public static GraphPalette For(ThemeVariant variant) {
        for (ThemeVariant? current = variant; current is not null; current = current.InheritVariant) {
            if (current == ThemeVariant.Dark) {
                return Dark;
            }
            if (current == ThemeVariant.Light) {
                return Light;
            }
        }
        return Light;
    }

    // получаем заливку, обводку и русское название вида узла
    public (IBrush Fill, IBrush Border, string Kind) ForKind(string kind) => kind switch {
        "class" => (ClassFill, ClassBorder, "класс"),
        "instance" => (InstanceFill, InstanceBorder, "экземпляр"),
        _ => (ValueFill, ValueBorder, "значение")
    };
}
