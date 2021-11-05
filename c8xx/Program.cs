using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SDL2;


using static SDL2.SDL;
using static SDL2.SDL_ttf;

namespace c8xx
{
	class Program
	{

		static C8VM vm;

		static Dictionary<SDL_Keycode, int> KeyboardMap = new Dictionary<SDL_Keycode, int>(){
			{SDL_Keycode.SDLK_1,   0x1},
			{SDL_Keycode.SDLK_2,   0x2},
			{SDL_Keycode.SDLK_3,   0x3},
			{SDL_Keycode.SDLK_4,    0xC},
			{SDL_Keycode.SDLK_q,    0x4},
			{SDL_Keycode.SDLK_w,    0x5},
			{SDL_Keycode.SDLK_e,    0x6},
			{SDL_Keycode.SDLK_r,    0xD},
			{SDL_Keycode.SDLK_a,    0x7},
			{SDL_Keycode.SDLK_s,    0x8},
			{SDL_Keycode.SDLK_d,    0x9},
			{SDL_Keycode.SDLK_f,    0xE},
			{SDL_Keycode.SDLK_z,    0xA},
			{SDL_Keycode.SDLK_x,    0x0},
			{SDL_Keycode.SDLK_c,    0xB},
			{SDL_Keycode.SDLK_v,    0xF},
		};
		[STAThread]
		static void Main(string[] args)
		{
			Renderer rend = new Renderer("nes_font.ttf", "c8xx - EightyVice", 570, 450);
			vm = new C8VM(File.ReadAllBytes("main.ch8"));
			string last_rom_place = "main.ch8";
			Console.WriteLine("Initial ROM Loaded");


			SDL_Event sdlEvent;
			bool running = true;
			bool emulate = false;

			int sample = 0;
			int beepSamples = 0;

			SDL_AudioSpec audioSpec = new SDL.SDL_AudioSpec();
			audioSpec.channels = 1;
			audioSpec.freq = 44100;
			audioSpec.samples = 256;
			audioSpec.format = AUDIO_S8;
			audioSpec.callback = new SDL.SDL_AudioCallback((userdata, stream, length) =>
			{
				if (vm == null) return;

				sbyte[] waveData = new sbyte[length];

				for (int i = 0; i < waveData.Length && vm.SoundTimer > 0; i++, beepSamples++)
				{
					if (beepSamples == 730)
					{
						beepSamples = 0;
						vm.SoundTimer--;
					}

					waveData[i] = (sbyte)(127 * Math.Sin(sample * Math.PI * 2 * 604.1 / 44100));
					sample++;
				}

				byte[] byteData = (byte[])(Array)waveData;

				Marshal.Copy(byteData, 0, stream, byteData.Length);
			});

			SDL_OpenAudio(ref audioSpec, IntPtr.Zero);
			SDL_PauseAudio(0);

			while (running)
			{


				while (SDL_PollEvent(out sdlEvent) != 0)
				{
					switch (sdlEvent.type)
					{
						
						case SDL_EventType.SDL_QUIT:
							running = false;
							break;

						case SDL_EventType.SDL_KEYDOWN:
							var keyup = sdlEvent.key.keysym.sym;
							if (KeyboardMap.ContainsKey(keyup) && emulate)
							{
								vm.Keyboard[KeyboardMap[keyup]] = true;
								vm.SendKey(KeyboardMap[keyup]);
							}
							break;

						case SDL_EventType.SDL_KEYUP:

							switch (sdlEvent.key.keysym.sym)
							{
								case SDL_Keycode.SDLK_F2:
									emulate = false;
									OpenFileDialog ofd = new OpenFileDialog();
									if (ofd.ShowDialog() == DialogResult.OK)
									{
										vm = new C8VM(File.ReadAllBytes(ofd.FileName));
										Console.WriteLine($"**********\n[LOG] Loadded ROM {ofd.FileName}\nSize: {new FileInfo(ofd.FileName).Length} bytes\n**********");
										last_rom_place = ofd.FileName;
										//rend.Disassembly = vm.Disassemble().ToArray();
										emulate = true;
									}
									break;
								case SDL_Keycode.SDLK_F3:
									emulate = false;
									vm = new C8VM(File.ReadAllBytes(last_rom_place));
									Console.WriteLine($"[LOG] ROM Reloaded");
									emulate = true;
									break;
								default:
									{
										var keydown = sdlEvent.key.keysym.sym;

										if (KeyboardMap.ContainsKey(keydown) && emulate)
										{
											vm.Keyboard[KeyboardMap[keydown]] = false;
										}
										break;

									}
							}

							break;

					}

				}


				if (vm != null)
				{
					vm.EmulateCycle();
					rend.Draw(vm);
				}

				rend.Present();
								
				Thread.Sleep(1);
			}
		}

	}
}
