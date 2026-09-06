using SDL3;
using ZX_81_Net;

[SDL.GenerateMain]
internal sealed partial class Game : SDL.IMainCallbacks<Game>
{
    private readonly Configuration _configuration = new();

    private readonly Cabinet _computer;

    private EightBit.ILogger Logger => this._computer.Logger;

    public Game()
    {
        this._computer = new(this._configuration);
    }

    private void LoadROM()
    {
        var romDirectory = this._configuration.RomDirectory;
    }

    private void LoadProgram()
    {
        var programDirectory = this._configuration.ProgramDirectory;
    }

    public static SDL.AppResult AppInit(out Game? appState, string[] args)
    {
        appState = new Game();
        appState._computer.RaisePOWER();

        appState.LoadROM();
        appState.LoadProgram();

        appState.Logger.Inform("Completed application initialisation");

        return SDL.AppResult.Continue;
    }

    public void AppQuit(SDL.AppResult result)
    {
        this.Logger.Inform("Terminating application");
        this._computer.LowerPOWER();
    }

    public SDL.AppResult AppIterate()
    {
        this.Logger.Inform("Executing application frame");
        return this._computer.RunFrame();
    }

    public SDL.AppResult AppEvent(ref SDL.Event @event)
    {
        this.Logger.Inform("Handling application event");
        return this._computer.HandleEvent(@event);
    }
}
