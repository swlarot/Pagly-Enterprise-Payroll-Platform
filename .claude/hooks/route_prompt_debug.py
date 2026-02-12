#!/usr/bin/env python3
"""
Debug version of UserPromptSubmit hook to diagnose errors
"""

import sys
import json
from datetime import datetime

def main():
    """Main hook execution with detailed logging."""
    log_file = ".claude/hooks/debug.log"

    try:
        # Log the timestamp
        with open(log_file, "a", encoding="utf-8") as f:
            f.write(f"\n\n=== {datetime.now().isoformat()} ===\n")
            f.write("Hook started\n")

        # Read input
        input_data = json.load(sys.stdin)

        with open(log_file, "a", encoding="utf-8") as f:
            f.write(f"Input received: {json.dumps(input_data, indent=2)}\n")

        # Simple output - no emojis, minimal text
        result = {
            'hookSpecificOutput': {}
        }

        with open(log_file, "a", encoding="utf-8") as f:
            f.write(f"Output: {json.dumps(result)}\n")
            f.write("Hook completed successfully\n")

        # Output to stdout
        print(json.dumps(result))

    except Exception as e:
        with open(log_file, "a", encoding="utf-8") as f:
            f.write(f"ERROR: {str(e)}\n")
            f.write(f"Type: {type(e).__name__}\n")
            import traceback
            f.write(f"Traceback:\n{traceback.format_exc()}\n")

        # Even on error, output valid JSON
        print(json.dumps({'hookSpecificOutput': {}}))

if __name__ == '__main__':
    main()
