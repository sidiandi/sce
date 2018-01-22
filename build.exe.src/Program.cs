using System;
using System.Threading.Tasks;

class Target
{
  private readonly Targets Targets;

  public Target(Targets targets)
  {
    this.targets = targets;
  }

  public Target DependsOn(Target t)
  {
    return this;
  }

  public Target Does(Func<Task> a)
  {
    does = a;
    return this;
  }

  Func<Task> does;
  private readonly Targets targets;

}

class Targets
{
  public Target Target(Func<Task> a)
  {
    return new Target(this).Does(a);
  }

  public Target Target()
  {
    return new Target(this);
  }

  public void Run(Target target)
  {

  }
}

#pragma warning disable 1998

class Program
{
    static async Task Msbuild(string solutionFile)
    {

    }

    static async Task Nunit(string solutionFile)
    {

    }

    async Task Build()
    {
      await Msbuild("sce.sln");
    }

    async Task Test()
    {
      await Nunit(".");
    }

    internal static void Main(string[] args)
    {
      var product = "sce";
      var solution = product + ".sln";

		  var t = new Targets();

      var build = t.Target(async () =>
      {
        await Msbuild(solution);
      });

      var test = t.Target(async () => {
      }).DependsOn(build);

      var packChocolatey = t.Target(async() => {
      }).DependsOn(build);

      var all = t.Target().DependsOn(packChocolatey);

      packChocolatey.DependsOn(test);

      t.Run(all);
    }
}
 