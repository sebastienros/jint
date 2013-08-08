/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-305.js
 * @description Object.create defines a data property when one property in 'Properties' is generic descriptor (8.12.9 step 4.a)
 */


function testcase() {

        try {
            var newObj = Object.create({}, {
                prop: {
                    enumerable: true
                }
            });
            return newObj.hasOwnProperty("prop");
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
