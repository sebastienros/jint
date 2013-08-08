/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * create sets the [[Prototype]] of the created object to first parameter.
 * This can be checked using isPrototypeOf, or getPrototypeOf.
 *
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-3-1.js
 * @description Object.create sets the prototype of the passed-in object
 */


function testcase() {
    function base() {}
    var b = new base();
    var d = Object.create(b);

    if (Object.getPrototypeOf(d) === b &&
        b.isPrototypeOf(d) === true) {
      return true;
    }
 }
runTestCase(testcase);
