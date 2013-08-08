/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-57.js
 * @description Object.defineProperties - value of 'enumerable' property of 'descObj' is a string (value is 'false') which is treated as true value (8.10.5 step 3.b)
 */


function testcase() {

        var obj = {};
        var accessed = false;

        Object.defineProperties(obj, {
            prop: {
                enumerable: "false"
            }
        });
        for (var property in obj) {
            if (property === "prop") {
                accessed = true;
            }
        }
        return accessed;
    }
runTestCase(testcase);
