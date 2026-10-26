// A call written in Ink by hand, for when JSON isn't enough. scream-line.json gives it a number.
// The game's lists, variables and functions are all available, plus the phone's own:
//   PhoneAct(action, values)    PhoneCheck(condition, value)
//   PhoneFlag(name)             SetPhoneFlag(name, value)    AddPhoneFlag(name, amount)
// End a call on the EndPhoneCall tag, like the game's calls do.

=== PhoneExpansion_ScreamLine ===
{~Welcome to the Scream Line!|You've reached the Scream Line.|Scream Line. Please hold your breath.}
{PhoneFlag("scream_line_calls") == 0: First time calling? Lucky you.}
~ AddPhoneFlag("scream_line_calls", 1)
Today's message:
{~Fun fact: the average house makes forty noises a night. Yours makes forty-one.|Fun fact: if you hear knocking, it's usually the door. Usually.|Fun fact: pumpkins are a fruit. So is whatever's in your basement.|Today's scream of the day: AAAAAAAAAAAAAAH. Thank you.}
* [Scream back]
    AAAAAAAAAAAAAAH!
    ...
    { PhoneCheck("holding", "Pumpkin"):
        Is that a pumpkin in your hand? Respect. Here's two.
        ~ PhoneAct("give", "item=Candy_Bar; count=2")
    - else:
        That was a good one. Here's a little something.
        ~ PhoneAct("give", "Candy_Bar")
    }
* [Hang up]
- # EndPhoneCall
-> DONE
