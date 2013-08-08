/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-59.js
 * @description Object.defineProperties - 'configurable' property of 'descObj' is present (8.10.5 step 4)
 */


function testcase() {

        var obj = {};

        Object.defineProperties(obj, {
            prop: {
                configurable: true
            }
        });

        var result1 = obj.hasOwnProperty("prop");
        delete obj.prop;
        var result2 = obj.hasOwnProperty("prop");

        return result1 === true && result2 === false;
    }
runTestCase(testcase);
