# OpenAI API Key Setup

## For Team Members

To enable AI-powered vulnerability classification, you need to add your OpenAI API key to the configuration.

### Steps:

1. **Get an OpenAI API Key** (if you don't have one):
   - Visit: https://platform.openai.com/account/api-keys
   - Sign in or create an account
   - Click "Create new secret key"
   - Copy the key (you'll only see it once!)

2. **Add the Key to Your Configuration**:
   
   **Option A: Use appsettings.Development.json** (Recommended for local development)
   - Open `BioShieldLens/appsettings.Development.json`
   - Add your API key to the `OpenAI.ApiKey` field:
   ```json
   "OpenAI": {
     "ApiKey": "sk-proj-YOUR_KEY_HERE"
   }
   ```

   **Option B: Use appsettings.json** (For production/shared environments)
   - Open `BioShieldLens/appsettings.json`
   - Add your API key to the `OpenAI.ApiKey` field

3. **Restart the Application**
   - The application will automatically detect the API key and enable AI classification

### Shared Team Key

If you want to use a shared team API key, contact the project owner to get the key via:
- Secure messaging (Slack, Teams, etc.)
- Password manager
- Encrypted file share

**⚠️ IMPORTANT: Never commit API keys to git!**

The `appsettings.json` file is excluded from version control to protect sensitive information.

