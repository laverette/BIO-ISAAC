# BioShield Lens - AI-Powered Vulnerability Analysis

## Quick Start

### Setting Up OpenAI API Key

To enable AI-powered vulnerability classification:

1. **Get the API Key from your team lead** (shared via secure channel)

2. **Add it to your configuration**:
   edit `appsettings.json`
   - Add your API key:
   ```json
   "OpenAI": {
     "ApiKey": "sk-proj-YOUR_KEY_HERE"
   }
   ```

3. **Restart the application**

See `API_KEY_SETUP.md` for detailed instructions.

### Authentication

The application requires authentication to access. To log in:

1. **Use an authorized email address**:
   - `@crimson.ua.edu`
   - `@bioisac.com`

2. **Enter the invitation code**: `BioShield2025!`

3. **Provide your name** when logging in

**Note**: Only users with authorized email domains and the correct invitation code can access the system.

## Features

- AI-powered vulnerability classification using OpenAI
- Automatic priority assignment (Critical, Monitor, Low Priority)
- Structured intelligence notes
- NVD API integration for vulnerability data
- Bulk classification support

## Important Notes

- The application will work without an API key (using keyword-based classification)
- AI classification requires a valid OpenAI API key

