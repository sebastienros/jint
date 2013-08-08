/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-3.js
 * @description Array.prototype.reduceRight returns initialValue if 'length' is 0 and initialValue is present (subclassed Array, length overridden to false (type conversion))
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  f.length = false;
  
  function cb(){}
  try {
    if(f.reduceRight(cb,1) === 1)
      return true;
  }
  catch (e) {  }  
 }
runTestCase(testcase);
