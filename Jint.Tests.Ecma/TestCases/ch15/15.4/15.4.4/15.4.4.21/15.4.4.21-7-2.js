/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-7-2.js
 * @description Array.prototype.reduce returns initialValue if 'length' is 0 and initialValue is present (subclassed Array, length overridden to null (type conversion))
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  f.length = null;
  
  function cb(){}
  try {
    if(f.reduce(cb,1) === 1)
      return true;
  }
  catch (e) {  }  
 }
runTestCase(testcase);
