// A call written in Ink by hand, for when JSON isn't enough. scream-line.json gives it a number.
// The game's lists, variables and functions are all available, plus the shared ones:
//   Act(action, values)    Check(condition, value)
//   Flag(name)             SetFlag(name, value)    AddFlag(name, amount)
// End a call on the EndPhoneCall tag, like the game's calls do.

=== PhoneExpansion_ScreamLine ===
{~<size=150%>SCREAM LINE!!!<size=100%> Wat do u want|u have reach the Scream Line. Pls hold ur breath|Scream Line. <size=60%>oh god its him again}
{Flag("scream_line_calls") == 0: First time calling? Lucky u. Most people die before the beep.}
~ AddFlag("scream_line_calls", 1)
Today's message:
{~Fun fact: the average house make forty noise a night. Yours make forty-one and one of them is u farting.|Fun fact: if u hear knocking its usually the door. Usually.|Fun fact: pumpkin is a fruit. So is whatever is growing in ur basement.|Scream of the day: <size=150%>AAAAAAAAAAAAAAAAAH<size=100%>. Is from a man who zip his pennis in his jeans. Respect.|Fun fact: if u scream in the shower nobody can hear it. U would know if u ever showered, u stinky son of a bitch.}
* [AAAAAAAAAAAAH]
    <size=150%>AAAAAAAAAAAAAAAAAAAAAAAH!!!
    <size=100%>...
    { Check("holding", "Pumpkin"):
        Is that a pumpkin in ur hand? Respect. Here is two.
        ~ Act("give", "item=Candy_Bar; count=2")
    - else:
        Shit that was a good one. My ears are bleeding and my balls went up inside me. Here.
        ~ Act("give", "Candy_Bar")
    }
* [Hang up]
    <size=60%>pussy
- # EndPhoneCall
-> DONE
