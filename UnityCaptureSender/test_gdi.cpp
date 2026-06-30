#include <windows.h>
#include <gdiplus.h>
#include <iostream>
using namespace std;
using namespace Gdiplus;

#pragma comment(lib, "gdiplus.lib")

int main()
{
	cout << "Test 1: Basic output works\n";

	cout << "Test 2: Initializing GDI+...\n";
	GdiplusStartupInput gdiplusStartupInput;
	ULONG_PTR gdiplusToken;
	Status status = GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, nullptr);

	if (status == Ok)
	{
		cout << "GDI+ initialized successfully\n";
		GdiplusShutdown(gdiplusToken);
	}
	else
	{
		cout << "GDI+ initialization failed with status: " << status << "\n";
	}

	cout << "All tests passed!\n";
	return 0;
}
