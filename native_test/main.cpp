#include <Windows.h>

using LoadMapFunc = int();

int main(int arg_count, char* args[])
{
	HMODULE loader_lib = LoadLibraryA("sledge_map_loader/bin/Debug/net8.0/publish/sledge_map_loader.dll");
	LoadMapFunc* load_map_func = reinterpret_cast<LoadMapFunc*>(GetProcAddress(loader_lib, "load_map"));
	int result = load_map_func();
}