using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SDL2;


using static SDL2.SDL;
using static SDL2.SDL_ttf;

namespace c8xx
{
	class Renderer
	{
		public IntPtr renderer { get; private set; }

		IntPtr _window;
		IntPtr _surface;
		IntPtr _texture;
		C8VM _vm;
		Font _font;
		public string[] Disassembly { get; set; }

		readonly SDL_Color COLOR_WHITE = new SDL_Color { r = 255, g = 255, b = 255, a = 255 };
		readonly SDL_Color COLOR_BLACK = new SDL_Color { r = 0, g = 0, b = 0, a = 255 };
		readonly SDL_Color COLOR_RED = new SDL_Color { r = 255, g = 0, b = 0, a = 255 };
		readonly SDL_Color COLOR_YELLOW = new SDL_Color { r = 252, g = 219, b = 3, a = 255 };

		public Renderer(string fontPath, string windowTitle, int width, int height, int xPos = SDL_WINDOWPOS_UNDEFINED, int yPos = SDL_WINDOWPOS_UNDEFINED)
		{
			if (SDL_Init(SDL_INIT_EVERYTHING) < 0)
			{
				Console.WriteLine("SDL failed to init.");
				return;
			}

			TTF_Init();
			_window = SDL_CreateWindow(windowTitle, xPos, yPos, width, height, 0);
			renderer = SDL_CreateRenderer(_window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
			SDL_SetRenderDrawColor(renderer, 0,  0,  0, 255);
			SDL_RenderClear(renderer);
			_font = new Font(fontPath);
		}

		public void Present() { 
			SDL_RenderPresent(renderer);
		}

		private void DrawText(Font font, string text, int x, int y, SDL_Color color)
		{
			_surface = TTF_RenderText_Blended_Wrapped(font.font, text, color, 600);
			_texture = SDL_CreateTextureFromSurface(renderer, _surface);
			SDL_FreeSurface(_surface);
			int w, h;
			SDL_QueryTexture(_texture, out _, out _, out w, out h);
			var rect = new SDL_Rect() { w = w, h = h, x = x, y = y };
			SDL_RenderCopy(renderer, _texture, IntPtr.Zero, ref rect);
			SDL_DestroyTexture(_texture);
		}

		private void DrawDisplay()
		{
			SDL_RenderClear(renderer);
			var displayHandle = GCHandle.Alloc(_vm.RGBABuffer, GCHandleType.Pinned);
			IntPtr surface = SDL_CreateRGBSurfaceFrom(displayHandle.AddrOfPinnedObject(), 64, 32, 32, 64 * 4, 0, 0, 0, 0);
			IntPtr texture = SDL_CreateTextureFromSurface(renderer, surface);
			var rect = new SDL_Rect() { w = 512, h = 256, x = 28, y = 20 };
			SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref rect);
			SDL_DestroyTexture(texture);
			displayHandle.Free();
		}

		private void DrawCPU()
		{
			DrawText(_font, "-- CPU --", 20, 300, COLOR_RED);
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < 4; i++)
			{
				sb.AppendLine($"V{i,-2}: {_vm.V[i],-3} V{i + 4,-2}: {_vm.V[i + 4],-3} V{i + 8,-2}: {_vm.V[i + 8],-3} V{i + 12,-2}: {_vm.V[i + 12],-3}");
			}
			sb.AppendLine($"ST : {_vm.SoundTimer,-3}  DT : {_vm.DelayTimer,-3}");
			sb.AppendLine($"I : 0x{_vm.I:X4}      PC : 0x{_vm.PC:X4}");
			DrawText(_font, sb.ToString(), 20, 320, COLOR_WHITE);
		}

		private void DrawDisassembly()
		{
			string dsm = _vm.GetCurrentInstructionDisassembly();
			DrawText(_font, $"Instruction: {dsm}", 20, 420, COLOR_YELLOW);
		}
		public void Draw(C8VM c8)
		{
			_vm = c8;	
			DrawDisplay();
			DrawCPU();
			DrawDisassembly();
		}
	}

	class Font : IDisposable
	{
		public IntPtr font;
		public Font(string Path) {
			font = TTF_OpenFont(Path, 16);
			if (font == IntPtr.Zero)
				Console.WriteLine($"[ERROR] Can't load the font {SDL_GetError()}");
		}

		public void Dispose()
		{
			TTF_CloseFont(font);
		}
	}
}
