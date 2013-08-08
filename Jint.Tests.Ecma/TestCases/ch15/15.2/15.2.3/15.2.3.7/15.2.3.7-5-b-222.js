/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-222.js
 * @description Object.defineProperties - value of 'get' property of 'descObj' is applied to Array object (8.10.5 step 7.b)
 */


function testcase() {
        var obj = {};

        try {
            Object.defineProperties(obj, {
                property: {
                    get: []
                }
            });

            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
