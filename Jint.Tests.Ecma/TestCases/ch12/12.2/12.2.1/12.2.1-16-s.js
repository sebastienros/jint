/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-16-s.js
 * @description A Function constructor (called as a function) declaring a var named 'arguments' does not throw a SyntaxError in strict mode
 * @onlyStrict
 */


    

function testcase() {
        'use strict';
        Function('var arguments;');
        return true;
}
runTestCase(testcase);