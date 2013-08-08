/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-21-s.js
 * @description StrictMode - reading a property named 'caller' of function objects is not allowed outside the function
 * @onlyStrict
 */



function testcase() {
        function foo () {"use strict";}
        try {
            var temp = foo.caller;
            return false;
        }
        catch (e) {
            return e instanceof TypeError;
        }
}
runTestCase(testcase);