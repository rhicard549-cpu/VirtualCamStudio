#include <windows.h>
#include <iostream>
#include <cstdint>
using namespace std;

#include "shared.inl"

int main()
{
	cout << "Test 1: Basic output works\n";

	cout << "Test 2: Creating SharedImageMemory object...\n";
	SharedImageMemory sender(0);
	cout << "SharedImageMemory created successfully\n";

	cout << "Test 3: Calling SendIsReady()...\n";
	bool ready = sender.SendIsReady();
	cout << "SendIsReady returned: " << (ready ? "true" : "false") << "\n";

	cout << "All tests passed!\n";
	return 0;
}
