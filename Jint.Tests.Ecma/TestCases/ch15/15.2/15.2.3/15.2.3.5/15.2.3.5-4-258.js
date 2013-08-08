/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-258.js
 * @description Object.create - 'get' property of one property in 'Properties' is the primitive value null (8.10.5 step 7.b)
 */


function testcase() {
        try {
            Object.create({}, {
                prop: {
                    get: null
                }
            });

            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
