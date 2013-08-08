/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-267.js
 * @description Object.create - 'set' property of one property in 'Properties' is not present (8.10.5 step 8)
 */


function testcase() {
        var newObj = Object.create({}, {
            prop: {
                get: function () {
                    return "data";
                }
            }
        });

        var hasProperty = newObj.hasOwnProperty("prop");

        newObj.prop = "overrideData";

        return hasProperty && newObj.prop === "data";
    }
runTestCase(testcase);
