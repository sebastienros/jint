/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * create sets the [[Prototype]] of the created object to first parameter.
 * This can be checked using isPrototypeOf, or getPrototypeOf.
 *
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-2-1.js
 * @description Object.create creates new Object
 */


function testcase() {
    function base() {}
    var b = new base();
    var prop = new Object();
    var d = Object.create(b);

    if (typeof d === 'object') {
      return true;
    }
 }
runTestCase(testcase);
