// An email written in Ink by hand, for when JSON isn't enough. from-mom.json gives it a subject.
// The shared functions work here: Act(action, values), Check(condition, value), Flag(name),
// SetFlag(name, value), AddFlag(name, amount). Choices are replies; [brackets] keep the reply
// from showing up again as a line.

=== EmailExpansion_FromMom ===
Hey sweetie its mom.
{Flag("mom_emails") > 0: Its mom again. U never call. U never shower. U just eat.}
~ AddFlag("mom_emails", 1)
I put money on ur card so u can buy snacks and not leave ur room. Dont tell ur father.
~ Act("money", "15")
+ [Thanks mom.]
    U are welcome. I love u even if u smell.^nxox
+ [Can I have more?]
    { Check("money", ">= 100"):
        U have {Check("money", ">= 500"): so much|enough} already. No.
    - else:
        Ok ok. Here. Dont tell ur father. Again.
        ~ Act("money", "10")
    }
- -> DONE
