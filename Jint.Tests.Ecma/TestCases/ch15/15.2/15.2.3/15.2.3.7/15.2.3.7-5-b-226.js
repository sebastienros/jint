/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-226.js
 * @description Object.defineProperties - 'set' property of 'descObj' is present (8.10.5 step 8)
 */


function testcase() {
        var data = "data";
        var obj = {};

        Object.defineProperties(obj, {
            "prop": {
                set: function (value) {
                    data = value;
                }
            }
        });

        obj.prop = "overrideData";

        return obj.hasOwnProperty("prop") && data === "overrideData";
    }
runTestCase(testcase);
