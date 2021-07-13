﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CMSlib.ConsoleModule.InputStates;
using CMSlib.Extensions;
using Microsoft.Extensions.Logging;

namespace CMSlib.ConsoleModule
{
    public sealed class ModuleManager : ILoggerFactory, IEnumerable<ModulePage>
    {
        public List<ModulePage> Pages { get; } = new();
        internal int selected = 0;
        internal object writeLock = new();
        
        private readonly object dictSync = new();

        private const string Ctrl = "Control";
        private const string Alt = "Alt";
        private const string Shift = "Shift";

        
        public ModuleManager()
        {
            Console.TreatControlCAsInput = true;
            IConsoleHelper helper;
            if (Environment.OSVersion.Platform.ToString().ToLower().Contains("win"))
            {
                
                helper = new WinConsoleHelper();
                helper.SetupConsole();
            }
            else
            {
                helper = new StdConsoleHelper();
            }
            Console.CancelKeyPress += (_, _) => { helper.QuitApp(null); };

            Console.Write(AnsiEscape.AlternateScreenBuffer);
            Console.Write(AnsiEscape.DisableCursorBlink);
            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        BaseModule selectedModule = SelectedModule;
                        InputModule inputModule = selectedModule as InputModule;
                        try
                        {
                            var inputRecord = helper.ReadInput();
                            await HandleInputAsync(inputRecord, selectedModule, helper);
                        }
                        catch (Exception exception)
                        {
                            inputModule?.AddText(exception.ToString());
                            inputModule?.WriteOutput();
                        }
                    }
                }
                catch (Exception e)
                {
                    helper.QuitApp(e);
                }
            });
        }

        public void Add(ModulePage page)
        {
            lock (dictSync)
            {
                page.SetParent(this);
                Pages.Add(page);
                
            }
        }

        public IEnumerator<ModulePage> GetEnumerator()
        {
            return Pages.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        

        /// <summary>
        /// The currently selected module. Returns null if there is no module currently selected;
        /// </summary>

        public BaseModule? SelectedModule

            => selected >= 0 && selected < Pages.Count ? Pages[selected].SelectedModule : null;
        

        private Queue<BaseModule> loggerQueue = new();

        /// <summary>
        /// The currently selected module that has input enabled. Returns null if there isn't one.
        /// </summary>
        public BaseModule? InputModule => SelectedModule as InputModule;

        public ModulePage? SelectedPage
        {
            get { lock(dictSync) return selected < 0 || selected >= Pages.Count ? null : Pages[selected]; }
        }

        /// <summary>
        /// The title of the current input module
        /// </summary>
        public string? InputModuleTitle
        {
            get => InputModule?.Title;
        }
        /// <summary>
        /// Refreshes all modules in this manager, ensuring that the latest output is displayed in all of them.
        /// </summary>
        /// <param name="clear">Whether to clear the console before writing.</param>
        public void RefreshAll(bool clear = true)
        {
            if (clear) Console.Clear();
            ModulePage selectedPage = SelectedPage;
            if (selectedPage is null) return;
            selectedPage.RefreshAll(false);
        }
        /// <summary>
        /// Refreshes a module by its title. This ensures that the latest output is displayed.
        /// </summary>
        /// <param name="title">The title of the module to refresh</param>
        /// <returns>Whether this operation was successful. It will not succeed if this manager does not have a module with the supplied title.</returns>
        public bool RefreshModule(string title)
        {
            BaseModule module = GetModule(title);
            if (module is null) return false;
            module.WriteOutput();
            return true;
        }
        /// <summary>
        /// Removes a module by its title.
        /// </summary>
        /// <param name="title"></param>
        /// <returns>Whether this operation was successful. It will not succeed if this manager does not have a module with the supplied title.</returns>
        public bool RemoveModule(string title)
        {
            return false;
            //TODO shift selected back down once module is removed
            /*
            bool success;
            lock (dictSync)
            {
                dictKeys.Remove(title);
                success = modules.Remove(title);
                if (selected >= dictKeys.Count)
                {
                    selected = -1;
                }
            }
            return success;
            */

        }
        
        
        /// <summary>
        /// Adds a module to the logger queue by title. This queue is accessed when this is called to CreateLogger.
        /// </summary>
        /// <param name="moduleTitle">The title of the module to add to the queue</param>
        /// <returns>Whether this operation was successful. It will not succeed if this manager does not have a module with the supplied title.</returns>
        public bool EnqueueLoggerModule(string moduleTitle)
        {
            BaseModule module = GetModule(moduleTitle);
            if(module is null) return false;
            loggerQueue.Enqueue(module);
            return true;
        }
        /// <summary>
        /// Adds a module to this manager.
        /// </summary>
        
        
        /// <summary>
        /// Gets a module by title
        /// </summary>
        /// <param name="title">The title of the module to get</param>
        public BaseModule this[string title] => GetModule(title);
        /// <summary>
        /// Gets a module by title
        /// </summary>
        /// <param name="title">The title of the module to get</param>
        /// <returns>The module with that title</returns>
        public BaseModule? GetModule(string title)
        {
            return Pages.FirstOrDefault(x => x.ContainsTitle(title))?[title];
        }

        /// <summary>
        /// Event fired when a line is entered in any module.
        /// </summary>
        public event AsyncEventHandler<LineEnteredEventArgs> LineEntered;
        /// <summary>
        /// Event fired when input is received.
        /// </summary>
        public event AsyncEventHandler<InputReceivedEventArgs> InputReceived;
        /// <summary>
        /// Event fired when input is received.
        /// </summary>
        public event AsyncEventHandler<KeyEnteredEventArgs> KeyEntered;

        /// <summary>
        /// Tries to get the next queued module to be used as a logger, and if the queue is empty return the input module.
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown when there are no modules created yet, and none in the queue.</exception>

        ILogger ILoggerFactory.CreateLogger(string categoryName)
        {
            lock (dictSync)
            {
                if (loggerQueue.TryDequeue(out var next))
                {
                    return next;
                }
                else if (Pages.Count > 0 && this.Pages.FirstOrDefault()?.FirstOrDefault() is BaseModule module)
                {
                    return module;
                }
                else
                {
                    throw new Exception("No modules created, and no modules queued.");
                }
            }
        }

        void ILoggerFactory.AddProvider(ILoggerProvider provider)
        {
            throw new NotImplementedException();
        }
        
        public void Dispose()
        {
            Pages.Clear();
            this.RefreshAll();
        }
        //todo add input for this too
        /// <summary>
        /// Selects the next module - enables scrolling for that module.
        /// </summary>
        public void SelectNext()
        {
            int newSelected;
            int pastSelected;
            bool refreshNew;
            bool refreshPast;
            ModulePage currentPage;
            lock (dictSync)
            {
                currentPage = Pages[selected];
                int currentSelected = currentPage.selected;
                pastSelected = currentSelected;
                currentSelected++;
                newSelected = (++currentSelected).Modulus(currentPage.Count + 1) - 1;
                currentPage.selected = newSelected;
                refreshPast = pastSelected >= 0;
                if (refreshPast)
                {
                    lock(currentPage.dictSync)
                        currentPage[pastSelected].selected = false;
                }
                
                refreshNew = newSelected >= 0;
                if (refreshNew)
                {
                    lock(currentPage.dictSync)
                        currentPage[newSelected].selected = true;
                }
            }
            if(refreshPast)
                currentPage[pastSelected].WriteOutput();
            if(refreshNew)
                currentPage[newSelected].WriteOutput();
        }

        public void NextPage()
        {
            lock(dictSync)
                selected = (++selected).Modulus(Pages.Count);
            RefreshAll();
        }
        public void PrevPage()
        {
            lock(dictSync)
                selected = (--selected).Modulus(Pages.Count);
            RefreshAll();
        }
        public void ToPage(int page)
        {
            lock (dictSync)
                selected = page;
            RefreshAll();
        }
        /// <summary>
        /// Selects the previous module - enables scrolling for that module.
        /// </summary>
        public void SelectPrev()
        {
            int newSelected;
            int pastSelected;
            bool refreshNew;
            bool refreshPast;
            ModulePage currentPage;
            lock (dictSync)
            {
                currentPage = Pages[selected];
                int currentSelected = currentPage.selected;
                pastSelected = currentSelected;
                newSelected = currentSelected.Modulus(currentPage.Count + 1) - 1;
                currentPage.selected = newSelected;
                refreshPast = pastSelected >= 0;
                if (refreshPast)
                {
                    currentPage[pastSelected].selected = false;
                }
                
                refreshNew = newSelected >= 0;
                if (refreshNew)
                {
                    currentPage[newSelected].selected = true;
                }
            }
            if(refreshPast)
                currentPage[pastSelected].WriteOutput();
            if(refreshNew)
                currentPage[newSelected].WriteOutput();
        }
        

        private async Task HandleInputAsync(InputRecord? input, BaseModule selectedModule, IConsoleHelper helper)
        {
            if (input is null)
                return;
            
            
            switch (input.Value.EventType)
            {
                case EventType.Key when input.Value.KeyEvent.bKeyDown:
                    ConsoleKeyInfo key = input.Value;
                    InputModule inputModule = selectedModule as InputModule;
            
                    AsyncEventHandler<KeyEnteredEventArgs> handler = KeyEntered;
                    KeyEnteredEventArgs e = new()
                    {
                        Module = selectedModule,
                        KeyInfo = key
                    };
                    if(handler is not null)
                        await handler(inputModule, e);
            
                    if (inputModule is not null)
                    {
                        await inputModule.FireKeyEnteredAsync(e);
                    }
            
                    await HandleKeyAsync(key, selectedModule, helper);
                    break;
                
                case EventType.Mouse when input.Value.MouseEvent.ButtonState != 0:
                    ModulePage page = SelectedPage;
                    if (page is null) return;
                    foreach (var module in page.Where(x=>input.Value.MouseEvent.MousePosition.Inside(x)))
                    {
                        module.HandleClickAsync(input.Value);
                    }
                    break;
            }
            
        }


        private async Task HandleKeyAsync(ConsoleKeyInfo key, BaseModule selectedModule, IConsoleHelper helper)
        {
            
            Dictionary<string, bool> mods = key.Modifiers.ToStringDictionary<ConsoleModifiers>();
            if (mods[Alt])
                return;
            InputModule inputModule = selectedModule as InputModule;
            switch (key.Key)
            {
                case ConsoleKey.V when mods[Ctrl]:
                    if(inputModule is null)return;
                    
                    if (Environment.OSVersion.Platform.ToString().ToLower().Contains("win"))
                    {
                        string clipboard = helper.GetClipboard();
                        foreach (var ch in clipboard.Replace("\r\n", "\n").Replace("\n", ""))
                        {
                            inputModule.AddChar(ch);
                        }
                    }
                    break;
                case ConsoleKey.C when mods[Ctrl]:
                    helper.QuitApp(null);
                    break;
                case ConsoleKey.RightArrow:
                    break;
                case ConsoleKey.LeftArrow:
                    break;
                case ConsoleKey.End when mods[Ctrl]:
                    selectedModule?.ScrollTo(0);
                    break;
                case ConsoleKey.Home when mods[Ctrl]:
                    selectedModule?.ScrollTo(int.MaxValue);
                    break;
                case ConsoleKey.PageUp:
                    selectedModule?.PageUp();
                    break;
                case ConsoleKey.PageDown:
                    selectedModule?.PageDown();
                    break;
                case ConsoleKey.UpArrow when mods[Ctrl]:
                    selectedModule?.ScrollUp(1);
                    break;
                case ConsoleKey.DownArrow when mods[Ctrl]:
                    selectedModule?.ScrollDown(1);
                    break;
                case ConsoleKey.UpArrow:
                    inputModule?.inputString?.Clear();
                    inputModule?.ScrollHistory(1);
                    break;
                case ConsoleKey.DownArrow:
                    inputModule?.inputString?.Clear();
                    inputModule?.ScrollHistory(-1);
                    break;
                case ConsoleKey.OemMinus when mods[Ctrl]:
                case ConsoleKey.Tab when mods[Ctrl] && mods[Shift]:
                    PrevPage();
                    break;
                case ConsoleKey.OemPlus when mods[Ctrl]:
                case ConsoleKey.Tab when mods[Ctrl]:
                    NextPage();
                    break;
                case ConsoleKey.Tab when mods[Shift]:
                    this.SelectPrev();
                    break;
                case ConsoleKey.Tab:
                    this.SelectNext();
                    break;
                
                case ConsoleKey.Enter when mods[Shift]:
                    await EnterLineAsync(inputModule, false);
                    break;
                case ConsoleKey.Enter:
                    await EnterLineAsync(inputModule, true);
                    return;
                case ConsoleKey.Backspace when inputModule?.inputString.Length.Equals(0) ?? false:
                    return;
                case ConsoleKey.Backspace when mods[Ctrl]:
                    goto NotImpl;
                    //TODO fix this
                    NotImpl:
                    break;
                case ConsoleKey.Backspace:
                    inputModule?.Backspace();
                    return;
                default:
                    inputModule?.AddChar(key.KeyChar);
                    
                    break;
            }
        }

        private async Task EnterLineAsync(InputModule inputModule, bool scrollToBottom)
        {
            if (inputModule is null) return;
            string line;
            AsyncEventHandler<LineEnteredEventArgs> handler;
            lock (this.writeLock)
            {
                handler = LineEntered;
                line = inputModule.inputString.ToString();
                inputModule.inputString.Clear();
                inputModule.lrCursorPos = 0;
                inputModule.AddToHistory(line);
                inputModule.usingHistory = false;
                if (scrollToBottom)
                {
                    inputModule.scrolledLines = 0;
                    inputModule.unread = false;
                }
            }
            var e = new LineEnteredEventArgs()
            {
                Module = inputModule,
                Line = line
            };
            inputModule.WriteOutput();
            if (handler != null)
                await handler(inputModule, e);
            await inputModule.FireLineEnteredAsync(e);
            await inputModule.FireReadLineLineEntered(e);
        }
    }
    /// <summary>
    /// EventArgs for the LineEntered Event
    /// </summary>
    public class LineEnteredEventArgs : EventArgs{
        internal LineEnteredEventArgs(){}
        /// <summary>
        /// The line that was inputted.
        /// </summary>
        public string Line { get; internal init; }
        
        public InputModule Module { get; internal init; }
    }
    /// <summary>
    /// EventArgs for the InputReceived Event
    /// </summary>
    public class InputReceivedEventArgs : EventArgs{
        internal InputReceivedEventArgs(){}
        /// <summary>
        /// Info about the input.
        /// </summary>
        public BaseInputState Input { get; internal init; }
    }

    public class MouseInputReceivedEventArgs : EventArgs
    {
        internal MouseInputReceivedEventArgs()
        {
        }
        public MouseMoveInputState InputState { get; internal init; }
    }
    public class KeyEnteredEventArgs : EventArgs {
        internal KeyEnteredEventArgs(){}
        /// <summary>
        /// Info about the input.
        /// </summary>
        public ConsoleKeyInfo? KeyInfo { get; internal init; }
        
        public BaseModule Module { get; internal init; }

        public int? InputLineLength
        {
            get
            {
                var IM = Module as InputModule;
                return IM?.inputString?.Length;
            }
        }
    }
    
    public delegate Task AsyncEventHandler<in T>(object sender, T eventArgs);
}
