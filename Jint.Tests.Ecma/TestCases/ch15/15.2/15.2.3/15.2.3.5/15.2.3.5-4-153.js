/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-153.js
 * @description Object.create - 'value' property of one property in 'Properties' is not present (8.10.5 step 5)
 */


function testcase() {

        var newObj = Object.create({}, {
            prop: {}
        });

        return newObj.hasOwnProperty("prop") && typeof (newObj.prop) === "undefined";
    }
runTestCase(testcase);
