/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-220.js
 * @description Object.defineProperties - value of 'get' property of 'descObj' is primitive values( value is number) (8.10.5 step 7.b)
 */


function testcase() {
        var obj = {};

        try {
            Object.defineProperties(obj, {
                property: {
                    get: 123
                }
            });

            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
