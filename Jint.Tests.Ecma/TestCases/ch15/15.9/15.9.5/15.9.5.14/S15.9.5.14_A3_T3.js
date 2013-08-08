// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Date.prototype.getDate property "length" has { ReadOnly, DontDelete, DontEnum } attributes
 *
 * @path ch15/15.9/15.9.5/15.9.5.14/S15.9.5.14_A3_T3.js
 * @description Checking DontEnum attribute
 */

if (Date.prototype.getDate.propertyIsEnumerable('length')) {
  $ERROR('#1: The Date.prototype.getDate.length property has the attribute DontEnum');
}

for(x in Date.prototype.getDate) {
  if(x === "length") {
    $ERROR('#2: The Date.prototype.getDate.length has the attribute DontEnum');
  }
}


