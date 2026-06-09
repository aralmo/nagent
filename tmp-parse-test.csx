using Nagent.Core.Parsing;
var parser = new TemplateParser();
var content = File.ReadAllText("agents/prompt-yesno-multiline-test.md");
try {
  var t = parser.ParseContent(content);
  foreach (var n in t.Nodes) Console.WriteLine(n);
} catch (Exception ex) {
  Console.WriteLine(ex.Message);
}
