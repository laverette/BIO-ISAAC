# BioShield Lens - AI-Powered Vulnerability Analysis

## Quick Start

### Setting Up OpenAI API Key

To enable AI-powered vulnerability classification:

1. **Get the API Key from your team lead** (shared via secure channel)

2. **Add it to your configuration**:
   - Copy `appsettings.example.json` to `appsettings.json` (if it doesn't exist)
   - OR edit `appsettings.Development.json`
   - Add your API key:
   ```json
   "OpenAI": {
     "ApiKey": "sk-proj-YOUR_KEY_HERE"
   }
   ```

3. **Restart the application**

See `API_KEY_SETUP.md` for detailed instructions.

## Features

- AI-powered vulnerability classification using OpenAI
- Automatic priority assignment (Critical, Monitor, Low Priority)
- Structured intelligence notes
- NVD API integration for vulnerability data
- Bulk classification support

## Important Notes

- **Never commit `appsettings.json`** - it contains sensitive API keys
- The application will work without an API key (using keyword-based classification)
- AI classification requires a valid OpenAI API key

