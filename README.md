# Partner Plus Points
Partner Plus Points is a .NET Console Application designed for retrieving and outputting your current Partner Plus Points count in real time.

## Features
* Outputs your Partner Plus points to a text file with a customizable string.
* Allows you to update your goal point count at any time.
* Can display Partner Plus points as a fluctuating count (like sub points) or just as an increasing number.
* Provides a simple command line interface for easily updating settings.
* Instantly updates the count on new subscriptions and tier upgrades.


## Installation and Usage
To use this program, just download and run the executable and then follow the instructions given. 

This program will create a settings file in the same directory to store your preferences. In addition, this program downloads Firefox to extract your API keys from, and this Firefox install is also stored in the same directory.

## Command Line Arguments
|     Option     |                                            Description                                              |
|----------------|-----------------------------------------------------------------------------------------------------|
|-h, --help      |Displays the help message with all command line arguments.                                           |
|-g, --gqltoken  |Uses the GQL auth token provided by the user. Use with -t to skip the browser.                       |
|-n, --nostore   |Sets the "store sensitive info" variable to false, which means your GQL auth token will not be saved.|
|-r, --reset     |Resets the program to its default state. Must be used by itself.                                     |
|-t, -twitchtoken|Uses the Twitch user access token provided by the user. Use with -g to skip the browser.             |

## How It Works
This program uses Twitch's undocumented GraphQL endpoint to retrieve your Partner Plus points count in real time. In addition, it also keeps track of new subscriptions by itself to be ahead of Twitch's own API. 

In the case of an incremental count, the following GQL query is used, with startDate being the current time and endDate being three months ago:
```
query {
    creatorProgramInfo(channelID:, endDate:, startDate:) {
        partnerPlusProgram {
            subPoints {
                count
            }
        }
    }
}
```

In the case of a fluctuating count, the following GQL query is used, with startDate being the current time and endDate being one month ago:
```
query {
    revenues(startAt:, endAt:, timeZone:"UTC", channelID:) {
        revenuePanel {
            paidSubscriptions {
                tierOneSubs {
                    subCount
                }
                tierTwoSubs {
                    subCount
                }
                tierThreeSubs {
                    subCount
                }
            }
        }
    }
}
```

## Supported Operating Systems
In theory, this program supports all versions of Windows, Mac OS X, and Linux. However, in my testing, I found that Twitch would not allow Firefox on Ubuntu (ARM) to sign in. In cases like these, you must manually provide your API keys using the command line arguments.

## Contributing
Contributions of any kind are more than welcome. Just make a pull request!

## License
This software is licensed under the [GNU GPL v3](https://choosealicense.com/licenses/gpl-3.0/)
