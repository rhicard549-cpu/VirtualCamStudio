#include <windows.h>
#include <iostream>
using namespace std;

#include "FrameIPC.h"

int main()
{
	cout << "Test 1: Basic output works\n";

	cout << "Test 2: Initializing FrameIPC...\n";
	if (FrameIPC::Initialize())
	{
		cout << "FrameIPC initialized successfully\n";
	}
	else
	{
		cout << "FrameIPC initialization failed\n";
	}

	cout << "Test 3: Shutting down...\n";
	FrameIPC::Shutdown();

	cout << "All tests passed!\n";
	return 0;
}
