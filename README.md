# ProcessSuspender

Console app for Windows to allow suspending a process by name for a specified number of seconds using a hotkey.

## Requirements

[.Net Framework 4.7.2](https://dotnet.microsoft.com/download/thank-you/net472)

## Install

Download the latest [release](https://github.com/SeanSobey/ProcessSuspender/releases), extract the files anywhere on your PC.

## Usage

### Configuration

If you open `ProcessSuspender.exe.config` in a text editor you will see this section containing settings:

```xml
<appSettings>
  <add key="Key" value="Oemplus"/>
  <add key="SuspendTime" value="5000"/>
  <add key="ProcessName" value="notepad"/>
    <!--<add key="ProcessID" value="1234"/>-->
</appSettings>
```

#### Key

This is name of the hotkey you wish to use. See the [full list](https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=netframework-4.7.2). Note only single keys are supported. Examples are `F12`, `Home` and `OemPipe`.

#### SuspendTime

The number of milliseconds to suspend the process for.

#### ProcessName

The name of the process, this can be seen in the Task Manager.

#### ProcessID

Optional and Advanced. If you wish to use process ID rather then uncomment this key and set the value to the desired process ID.

### Running

Execute the `ProcessSuspender.exe` after you have configured `ProcessSuspender.exe.config` as you like, make sure that these files are in the same folder.

When you wish to suspend the process hit the hotkey you chose and wait for the magic :D
