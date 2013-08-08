/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-4.js
 * @description Array.prototype.lastIndexOf returns -1 if 'length' is 0 (generic 'array' with length 0 )
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  f.length = 0;
  
 var i = Array.prototype.lastIndexOf.call({length: 0}, 1);
  
  if (i === -1) {
    return true;
  }
 }
runTestCase(testcase);
