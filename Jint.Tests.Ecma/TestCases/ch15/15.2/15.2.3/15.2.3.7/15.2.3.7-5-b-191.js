/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-191.js
 * @description Object.defineProperties - 'get' property of 'descObj' is present (8.10.5 step 7)
 */


function testcase() {
        var obj = {};

        var getter = function () {
            return "present";
        };

        Object.defineProperties(obj, {
            property: {
                get: getter
            }
        });

        return obj.property === "present";
    }
runTestCase(testcase);
