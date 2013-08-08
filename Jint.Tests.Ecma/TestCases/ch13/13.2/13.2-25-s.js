/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-25-s.js
 * @description StrictMode - reading a property named 'arguments' of function objects is not allowed outside the function
 * @onlyStrict
 */



function testcase() {
        function foo () {"use strict";}
        try {
            var temp = foo.arguments;
            return false;
        }
        catch (e) {
            return e instanceof TypeError;
        }
}
runTestCase(testcase);