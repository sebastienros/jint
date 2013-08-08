/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-266.js
 * @description Object.create - 'set' property of one property in 'Properties' is present (8.10.5 step 8)
 */


function testcase() {
        var data = "data";

        var newObj = Object.create({}, {
            prop: {
                set: function (value) {
                    data = value;
                }
            }
        });

        var hasProperty = newObj.hasOwnProperty("prop");

        newObj.prop = "overrideData";

        return hasProperty && data === "overrideData";
    }
runTestCase(testcase);
