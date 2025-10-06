#!/usr/bin/env python3
"""
Diagnostic script for Python execution issues
"""
import sys
import os

print("Python Diagnostic:")
print(f"Python version: {sys.version}")
print(f"Current working directory: {os.getcwd()}")
print(f"Python executable: {sys.executable}")
print("Python execution works correctly!")