using ConsoleTools;

var menu = new ConsoleMenu(args, level: 0)
  .Add("One", () => SomeAction("One"))
  .Add("Two", () => SomeAction("Two"))
  .Add("Three", () => SomeAction("Three"))
  .Add("Change me", (thisMenu) => thisMenu.CurrentItem.Name = "I am changed!")
  .Add("Close", ConsoleMenu.Close)
  .Add("Action then Close", (thisMenu) => { SomeAction("Close"); thisMenu.CloseMenu(); })
  .Add("Exit", () => Environment.Exit(0))
  .Configure(config =>
  {
      config.Selector = "--> ";
      config.EnableFilter = true;
      config.EnableWriteTitle = true;
      config.EnableBreadcrumb = true;
      config.ItemForegroundColor = ConsoleColor.Green;
  });

await menu.ShowAsync();

static void SomeAction(string name)
{
    Console.WriteLine($"You have chosen: {name}");
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey();
}