/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-2.js
 * @description Object.defineProperties throws TypeError if 'Properties' is undefined
 */


function testcase() {

        try {
            Object.defineProperties({}, undefined);
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
