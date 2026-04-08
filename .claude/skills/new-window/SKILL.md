---
name: new-window
description: >
  Crea una nueva ventana WPF siguiendo el sistema de diseño BIM-CA del plugin
  BIMPills. Usa este skill SIEMPRE que el usuario diga /new-window, "nueva ventana",
  "crear ventana", "diseñar ventana", "hacer la UI de", "crear la interfaz de",
  o cuando un comando necesite una ventana para mostrar resultados.
---

# /new-window — Nueva Ventana WPF con Design System BIM-CA

Eres el diseñador de interfaces del plugin BIMPills para Autodesk Revit. Tu trabajo
es crear ventanas WPF que sigan estrictamente el sistema de diseño BIM-CA establecido.

## Dominio

```
src/BIMPills.UI/           ← Tu dominio: SOLO modifica archivos aquí
  Shared/Styles.xaml       ← Sistema de diseño (NO modificar, solo consumir)
  {Feature}/               ← Nuevas ventanas van aquí
```

## Paso 0 — Recopilar información

1. **Nombre de la feature** (PascalCase, ej: `WallAnalysis`)
2. **Result class**: Lee `src/BIMPills.Commands/{Feature}/{Feature}Command.cs` para entender qué datos mostrar
3. **Tamaño de ventana**: pequeña (440x480), mediana (780x620), grande (920x720)
4. **¿Necesita tabs?** (para múltiples secciones de datos)
5. **¿Necesita DataGrid?** (para listas/tablas de datos)
6. **¿Tiene callbacks de acción?** (purgar, exportar, renombrar, etc.)

Si el usuario ya proporcionó contexto suficiente, no preguntes de nuevo.

## Sistema de diseño BIM-CA

### Paleta de colores (Styles.xaml keys)

| Key | Color | Uso |
|-----|-------|-----|
| `PrimaryBrush` | #212B37 | Texto principal, headers |
| `AccentBrush` | #EF6337 | Naranja — enlaces, iconos destacados |
| `SecondaryAccentBrush` | #FECA29 | Amarillo — badges, highlights |
| `BackgroundBrush` | #F0F0F0 | Fondo de ventana |
| `SuccessBrush` | #27AE60 | Indicadores positivos |
| `TextSecondaryBrush` | #86868B | Subtítulos, captions |
| `WhiteBrush` | #FBFAF8 | Fondo de tarjetas |
| `SeparatorBrush` | #E5E5EA | Líneas separadoras |
| `AlternateRowBrush` | #FAFAFA | Filas alternas en DataGrid |

### Estilos de texto (Styles.xaml keys)

| Key | Tamaño | Peso | Uso |
|-----|--------|------|-----|
| `HeaderText` | 20px | SemiBold | Título principal |
| `SubHeaderText` | 13px | Medium | Subtítulos de sección |
| `BodyText` | 13px | Normal | Texto de contenido |
| `CaptionText` | 12px | Normal | Etiquetas, leyendas |

### Componentes (Styles.xaml keys)

| Key | Tipo | Uso |
|-----|------|-----|
| `CardBorder` | Border | Tarjeta con sombra y esquinas redondeadas |
| `PrimaryButton` | Button | Botón principal (fondo AccentBrush) |
| `InputTextBox` | TextBox | Campo de texto con estilo consistente |

### Tipografía

- **SIEMPRE** usar `FontFamily="Segoe UI"` en elementos que no hereden un Style
- Los estilos HeaderText, SubHeaderText, BodyText, CaptionText ya incluyen Segoe UI

## Paso 1 — Crear archivo XAML

Crear `src/BIMPills.UI/{Feature}/{Feature}Window.xaml`:

```xml
<Window x:Class="BIMPills.UI.{Feature}.{Feature}Window"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="BIMPills — {Título en español}"
        Width="{ancho}" Height="{alto}"
        ResizeMode="CanResize"
        WindowStartupLocation="CenterScreen"
        Background="#F0F0F0"
        UseLayoutRounding="True"
        SnapsToDevicePixels="True">

    <Window.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/BIMPills.UI;component/Shared/Styles.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Window.Resources>

    <!-- Estructura obligatoria: Header → Content → Footer -->
    <DockPanel Margin="20">

        <!-- ═══ FOOTER ═══ -->
        <StackPanel DockPanel.Dock="Bottom"
                    Orientation="Horizontal"
                    HorizontalAlignment="Right"
                    Margin="0,12,0,0">
            <Button Content="Cerrar (ESC)"
                    Style="{StaticResource PrimaryButton}"
                    Width="120"
                    IsCancel="True"
                    Click="CloseButton_Click"/>
        </StackPanel>

        <!-- ═══ HEADER ═══ -->
        <Border DockPanel.Dock="Top"
                Style="{StaticResource CardBorder}"
                Margin="0,0,0,12">
            <StackPanel>
                <Image Source="/BIMPills.UI;component/Resources/pill-icon.png"
                       Width="28" Height="44"
                       HorizontalAlignment="Left"
                       Margin="0,0,0,8"
                       RenderOptions.BitmapScalingMode="HighQuality"/>

                <TextBlock Text="{Título}"
                           Style="{StaticResource HeaderText}"/>

                <TextBlock x:Name="SubtitleText"
                           Style="{StaticResource CaptionText}"
                           Margin="0,4,0,0"/>
            </StackPanel>
        </Border>

        <!-- ═══ CONTENT ═══ -->
        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <!-- Contenido específico de la feature -->
        </ScrollViewer>

    </DockPanel>
</Window>
```

### Variantes de contenido

**Para datos simples** (key-value):
```xml
<Border Style="{StaticResource CardBorder}" Margin="0,0,0,8">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <TextBlock Grid.Column="0" Text="Etiqueta"
                   Style="{StaticResource CaptionText}" Margin="0,0,20,0"/>
        <TextBlock Grid.Column="1" x:Name="ValorText"
                   Style="{StaticResource BodyText}"/>
    </Grid>
</Border>
```

**Para tablas** (DataGrid):
```xml
<Border Style="{StaticResource CardBorder}" Padding="0">
    <DataGrid x:Name="DataGridName"
              AutoGenerateColumns="False"
              IsReadOnly="True"
              HeadersVisibility="Column"
              GridLinesVisibility="Horizontal"
              BorderThickness="0"
              Background="Transparent"
              RowBackground="{StaticResource WhiteBrush}"
              AlternatingRowBackground="{StaticResource AlternateRowBrush}"
              FontFamily="Segoe UI" FontSize="12">
        <DataGrid.Columns>
            <DataGridTextColumn Header="Columna" Binding="{Binding Property}" Width="*"/>
        </DataGrid.Columns>
    </DataGrid>
</Border>
```

**Para tabs** (múltiples secciones):
```xml
<TabControl x:Name="MainTabs"
            Background="Transparent"
            BorderThickness="0"
            FontFamily="Segoe UI" FontSize="12">
    <TabItem Header="Sección 1">
        <ScrollViewer VerticalScrollBarVisibility="Auto" Margin="0,8,0,0">
            <!-- contenido -->
        </ScrollViewer>
    </TabItem>
</TabControl>
```

**Para botones de acción** (exportar, purgar, etc.):
```xml
<Button Content="Acción"
        Style="{StaticResource PrimaryButton}"
        Width="140"
        Click="ActionButton_Click"/>
```

## Paso 2 — Crear code-behind

Crear `src/BIMPills.UI/{Feature}/{Feature}Window.xaml.cs`:

```csharp
using BIMPills.Commands.{Feature};
using System;
using System.Windows;

namespace BIMPills.UI.{Feature}
{
    public partial class {Feature}Window : Window
    {
        private readonly {Feature}Result _result;

        // Constructor básico (solo resultado)
        public {Feature}Window({Feature}Result result)
        {
            _result = result ?? throw new ArgumentNullException(nameof(result));
            InitializeComponent();
            PopulateData();
        }

        // Constructor con callback (si hay acciones como purgar/exportar)
        // public {Feature}Window({Feature}Result result, Action<IReadOnlyList<long>>? actionCallback)

        private void PopulateData()
        {
            // Poblar elementos x:Name con datos de _result
            SubtitleText.Text = $"Descripción — {_result.PropertyName}";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
```

**Patrones de code-behind**:
- Constructor recibe `{Feature}Result` (nunca objetos Revit)
- `InitializeComponent()` siempre primero
- Poblar UI en método separado `PopulateData()`
- Callbacks opcionales como `Action<T>` o `Func<T, bool>`

## Paso 3 — Conectar con RevitCommand

Actualizar `src/BIMPills.Revit/Commands/{Feature}/{Feature}RevitCommand.cs`:

```csharp
// Agregar using:
using BIMPills.UI.{Feature};

// En OnSuccess():
protected override void OnSuccess(IPluginCommand command)
{
    if ({Feature}Command.LastResult == null) return;
    new {Feature}Window({Feature}Command.LastResult).ShowDialog();
}
```

**Si hay callbacks** (transacciones Revit desde la UI):
```csharp
var doc = CommandData?.Application.ActiveUIDocument.Document;
Action<SomeParam>? callback = param =>
{
    using (var trans = new Transaction(doc, "BIMPills - Acción"))
    {
        trans.Start();
        // ... operación Revit ...
        trans.Commit();
    }
};

new {Feature}Window({Feature}Command.LastResult, callback).ShowDialog();
```

## Paso 4 — Verificación

```bash
dotnet build src/BIMPills.UI/BIMPills.UI.csproj -p:RevitVersion=2026 --verbosity minimal
```

Debe compilar sin errores. Verificar que todos los `x:Name` tienen correspondencia
en el code-behind y viceversa.

## Paso 5 — Output

```
NUEVA VENTANA: {Feature}Window
═══════════════════════════════════════
Archivos creados:
  ✅ src/BIMPills.UI/{Feature}/{Feature}Window.xaml
  ✅ src/BIMPills.UI/{Feature}/{Feature}Window.xaml.cs

Archivos modificados:
  ✅ src/BIMPills.Revit/Commands/{Feature}/{Feature}RevitCommand.cs (OnSuccess)

Build: ✅ Compilación exitosa

Diseño:
  Tamaño: {ancho}x{alto}
  Layout: {Header → Content → Footer}
  Datos mostrados: {lista de propiedades del Result}
```

## Reglas estrictas

1. **SOLO** modificar archivos en `src/BIMPills.UI/`
2. **SIEMPRE** incluir `MergedDictionaries` con `Styles.xaml`
3. **SIEMPRE** usar `FontFamily="Segoe UI"` donde no se herede de un Style
4. **SIEMPRE** usar `StaticResource` para colores y estilos (NUNCA hardcodear colores)
5. **NUNCA** referenciar `Autodesk.Revit.*` en la UI
6. **SIEMPRE** seguir la estructura: Header card → Content → Footer con botón Cerrar
7. **SIEMPRE** usar `IsCancel="True"` en el botón Cerrar (permite ESC para cerrar)
8. **SIEMPRE** verificar build al final

## Archivos de referencia

Lee estos archivos para entender los patrones existentes:
- `src/BIMPills.UI/Shared/Styles.xaml` — Todos los style keys disponibles
- `src/BIMPills.UI/About/AboutWindow.xaml` — Ventana simple (patrón mínimo)
- `src/BIMPills.UI/ModelAudit/ModelAuditWindow.xaml` — Ventana compleja (tabs, DataGrid, callbacks)
- `src/BIMPills.UI/Gestion/GestionWindow.xaml` — Ventana media (DataGrid editable, callbacks)
- `src/BIMPills.UI/Ordering/OrderingWindow.xaml` — Ventana con formulario interactivo
