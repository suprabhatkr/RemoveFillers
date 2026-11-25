# Azure speech transcription

The media library can send an imported MP4, MP3, or WAV file to an Azure OpenAI transcription deployment. The result is displayed as text; it is not saved and does not change the timeline or remove filler words. The entire original file is uploaded, regardless of timeline splits or edits. Sample media and MOV files are not supported; convert MOV to MP4 first. The upload is limited to 25,000,000 bytes.

## Setup

1. Create an Azure OpenAI resource and deploy a supported speech-to-text model, such as Whisper or a transcription model supported by your resource and region.
2. Give your user identity permission to invoke the resource, such as the Cognitive Services OpenAI User role, and sign in with Azure CLI (`az login`). The app uses Azure CLI authentication; it does not store an API key.
3. Set `AZURE_OPENAI_ENDPOINT` to the resource endpoint (for example `https://your-resource.openai.azure.com/`) and `AZURE_OPENAI_TRANSCRIPTION_DEPLOYMENT` to the deployment name. Set them in the environment that launches Visual Studio, then restart Visual Studio so the app inherits them.
4. Import media, select it in the library, and choose **Transcribe with Azure**. Confirm the upload after reviewing the prompt. Azure usage may incur charges.

Only public Azure OpenAI resource endpoints ending in `.openai.azure.com` are accepted. The app uses the Azure OpenAI v1 audio transcription API. A valid Azure subscription, deployment, network access, and supported media are required. If a file exceeds the limit, use a smaller file or a separate batch transcription workflow.

See [Microsoft's transcription quickstart](https://learn.microsoft.com/en-us/azure/foundry/openai/whisper-quickstart) for deployment and authentication guidance.
