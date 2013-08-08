/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-301.js
 * @description Object.create - TypeError is thrown if both 'set' property and 'value' property of one property in 'Properties' are present (8.10.5 step 9.a)
 */


function testcase() {

        try {
            Object.create({}, {
                prop: {
                    set: function () { },
                    value: 100
                }
            });

            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
