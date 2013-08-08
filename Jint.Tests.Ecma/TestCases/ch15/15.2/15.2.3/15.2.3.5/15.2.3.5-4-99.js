/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-99.js
 * @description Object.create - 'configurable' property of one property in 'Properties' is true (8.10.5 step 4)
 */


function testcase() {

        var newObj = Object.create({}, {
            prop: {
                configurable: true
            }
        });

        var result1 = newObj.hasOwnProperty("prop");
        delete newObj.prop;
        var result2 = newObj.hasOwnProperty("prop");

        return result1 === true && result2 === false;
    }
runTestCase(testcase);
