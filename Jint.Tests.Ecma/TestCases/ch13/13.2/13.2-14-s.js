/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-14-s.js
 * @description StrictMode - writing a property named 'arguments' of function objects is not allowed outside the function
 * @onlyStrict
 */



function testcase() {
        var foo = new Function("'use strict';");
        try {
            foo.arguments = 41;
            return false;
        }
        catch (e) {
            return e instanceof TypeError;
        }
}
runTestCase(testcase);