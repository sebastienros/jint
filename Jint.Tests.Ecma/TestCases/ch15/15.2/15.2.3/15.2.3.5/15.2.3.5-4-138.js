/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-138.js
 * @description Object.create - 'configurable' property of one property in 'Properties' is an Array object (8.10.5 step 4.b)
 */


function testcase() {

        var newObj = Object.create({}, {
            prop: {
                configurable: []
            }
        });

        var beforeDeleted = newObj.hasOwnProperty("prop");

        delete newObj.prop;

        var afterDeleted = newObj.hasOwnProperty("prop");

        return beforeDeleted === true && afterDeleted === false;
    }
runTestCase(testcase);
