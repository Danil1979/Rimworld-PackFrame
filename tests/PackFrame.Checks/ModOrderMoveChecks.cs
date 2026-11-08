using Avalonia.Input;
using PackFrame;
internal static class ModOrderMoveChecks
{
 internal static void Run()
 {
  Check(new[]{"a","b","c","d"},new[]{"b","c"},-1,new[]{"b","c","a","d"},"contiguous selection moves up as a block");
  Check(new[]{"a","b","c","d"},new[]{"b","c"},1,new[]{"a","d","b","c"},"contiguous selection moves down as a block");
  Check(new[]{"a","b","c","d","e"},new[]{"b","d"},-1,new[]{"b","a","d","c","e"},"disjoint selected blocks retain their relative order");
  Check(new[]{"a","b","c","d"},new[]{"a","c"},-1,new[]{"a","c","b","d"},"top block stays while another block can move");
  Check(new[]{"a","b","c","d"},new[]{"b","d"},1,new[]{"a","c","b","d"},"bottom block stays while another block can move");
  Check(new[]{"visible","missing","filtered","target"},new[]{"target"},-1,new[]{"visible","missing","target","filtered"},"moves in full order rather than jumping over hidden entries");
  Check(new[]{"a","b","B","c"},new[]{"b"},1,new[]{"a","c","b","B"},"duplicate occurrences and original casing are preserved");
  Check(new[]{"a","b"},new[]{"unknown"},1,new[]{"a","b"},"unknown selection is a no-op");
  Check(new[]{"a","b"},new[]{"a","b"},-1,new[]{"a","b"},"all selected is a no-op");
  Check(Array.Empty<string>(),Array.Empty<string>(),-1,Array.Empty<string>(),"empty list is a no-op");
  Require(!ModOrderMove.CanMove(new[]{"a","b"},new[]{"a"},-1)&&!ModOrderMove.CanMove(new[]{"a","b"},new[]{"b"},1),"boundary buttons disable unavailable moves");
  for(var count=1;count<=7;count++)for(var mask=0;mask<(1<<count);mask++)foreach(var direction in new[]{-1,1})
  {
   var order=Enumerable.Range(0,count).Select(i=>i.ToString()).ToArray();var ids=order.Where((_,i)=>(mask&(1<<i))!=0).ToArray();
   var result=ModOrderMove.Apply(order,ids,direction);
   Require(result.Count==count&&result.Order().SequenceEqual(order.Order())&&result.Where(ids.Contains).SequenceEqual(ids)&&result.Where(x=>!ids.Contains(x)).SequenceEqual(order.Where(x=>!ids.Contains(x)))&&ModOrderMove.CanMove(order,ids,direction)==!order.SequenceEqual(result),$"move invariants n={count} selection={mask} direction={direction}");
  }
  using var markup = typeof(ModOrderMoveChecks).Assembly.GetManifestResourceStream("UiWiring.MainWindow.axaml")!;
  var document = System.Xml.Linq.XDocument.Load(markup);
  System.Xml.Linq.XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
  foreach (var pane in new[] { 1, 2 })
  {
   var up = document.Descendants().Single(e => (string?)e.Attribute(x + "Name") == $"MoveUp{pane}");
   var down = document.Descendants().Single(e => (string?)e.Attribute(x + "Name") == $"MoveDown{pane}");
   Require((string?)up.Attribute("Grid.Row") == "1" && (string?)down.Attribute("Grid.Row") == "1" &&
    (string?)up.Parent!.Attribute("ColumnDefinitions") == "*,*" && up.Attribute("Width") is null,
    "reorder controls leave a full-width enable/disable row in pane " + pane);
  }
  foreach(var language in new[]{"en","zh-Hans","zh-Hant","ja"})
   Require(ModOrderMove.ButtonLabel(language,-1)!=ModOrderMove.ButtonLabel(language,1)&&ModOrderMove.Label(language,-1)!=ModOrderMove.Label(language,1)&&ModOrderMove.Help(language).Contains("Alt+↑"),"localized direction and keyboard help in "+language);
  Require(ModPaneKeyboard.Resolve(Key.Up,KeyModifiers.Alt,true,false,true)==ModPaneKeyAction.MoveUp&&ModPaneKeyboard.Resolve(Key.Down,KeyModifiers.Alt,true,false,true)==ModPaneKeyAction.MoveDown,"Alt arrows reorder only eligible focused lists");
  Require(ModPaneKeyboard.Resolve(Key.Up,KeyModifiers.Alt,false,true,true)==ModPaneKeyAction.None&&ModPaneKeyboard.Resolve(Key.Up,KeyModifiers.Alt,true,false,false)==ModPaneKeyAction.None&&ModPaneKeyboard.Resolve(Key.Up,KeyModifiers.Alt|KeyModifiers.Shift,true,false,true)==ModPaneKeyAction.None,"search, available pane and unrelated modifiers retain their keys");
 }
 static void Check(string[] order,string[] ids,int direction,string[] expected,string label){var original=order.ToArray();Require(ModOrderMove.Apply(order,ids,direction).SequenceEqual(expected)&&order.SequenceEqual(original),label);}
 static void Require(bool value,string label){if(!value)throw new Exception("Failed: "+label);Console.WriteLine("PASS: "+label);}
}
