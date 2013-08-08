/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-205.js
 * @description Object.create - 'writable' property of one property in 'Properties' is null (8.10.5 step 6.b)
 */


function testcase() {

        var newObj = Object.create({}, {
            prop: {
                writable: null
            }
        });
        var hasProperty = newObj.hasOwnProperty("prop") && typeof newObj.prop === "undefined";

        newObj.prop = 121;

        return hasProperty && typeof newObj.prop === "undefined";
    }
runTestCase(testcase);
