/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-27-s.js
 * @description Strict Mode - TypeError is thrown after deleting a property, calling preventExtensions, and attempting to reassign the property
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var a = {x:0, get y() { return 0;}};
        delete a.x;
        Object.preventExtensions(a);
        try {
            a.x = 1;
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
}
runTestCase(testcase);
