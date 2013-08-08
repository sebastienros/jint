/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-233.js
 * @description Object.getOwnPropertyDescriptor - ensure that 'enumerable' property of returned object is data property with correct 'enumerable' attribute
 */


function testcase() {
        var obj = { "property": "ownDataProperty" };

        var desc = Object.getOwnPropertyDescriptor(obj, "property");
        var accessed = false;

        for (var props in desc) {
            if (props === "enumerable") {
                accessed = true;
            }
        }

        return accessed;
    }
runTestCase(testcase);
