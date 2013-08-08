/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.2/11.13.2-53-s.js
 * @description Strict Mode - TypeError is thrown if The LeftHandSide of a Compound Assignment operator(&=) is a reference to a non-existent property of an object whose [[Extensible]] internal property if false
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var obj = {};
        Object.preventExtensions(obj);

        try {
            obj.len &= 10;
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);
